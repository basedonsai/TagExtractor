using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OCRTool.Application;
using OCRTool.Core.Configuration;
using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;
using OCRTool.Infrastructure.Excel;
using OCRTool.Infrastructure.Logging;

namespace OCRTool.Services
{
    /// <summary>
    /// Main orchestration class that processes PDF files using OCR providers and PDF processors.
    /// Handles batch processing, progress reporting, cancellation, and result export.
    /// </summary>
    public class BatchProcessor
    {
        private readonly IOCRProvider _ocrProvider;
        private readonly IPDFProcessor _pdfProcessor;
        private readonly PatternMatcher _patternMatcher;
        private readonly ExcelExporter _excelExporter;
        private readonly FileLogger _logger;
        private readonly ExtractionConfig _config;
        
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalFiles;
        private int _processedFiles;
        private int _totalPages;
        private int _processedPages;

        /// <summary>
        /// Event raised when progress changes
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when processing completes
        /// </summary>
        public event EventHandler<ProcessingCompletedEventArgs>? ProcessingCompleted;

        /// <summary>
        /// Event raised for individual file processing updates
        /// </summary>
        public event EventHandler<FileProcessingEventArgs>? FileProcessing;

        /// <summary>
        /// Event raised for progress reporting
        /// </summary>
        public event EventHandler<ProgressReport>? ProgressUpdated;

        /// <summary>
        /// Creates a new BatchProcessor with the specified dependencies
        /// </summary>
        public BatchProcessor(
            IOCRProvider ocrProvider,
            IPDFProcessor pdfProcessor,
            PatternMatcher patternMatcher,
            ExcelExporter excelExporter,
            FileLogger logger,
            ExtractionConfig config)
        {
            _ocrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
            _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
            _patternMatcher = patternMatcher ?? throw new ArgumentNullException(nameof(patternMatcher));
            _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Creates a BatchProcessor with default implementations
        /// </summary>
        public static BatchProcessor Create(ConfigurationManager configManager, FileLogger logger)
        {
            var config = configManager.LoadConfiguration();
            var tessdataPath = configManager.GetTessdataPath();
            
            var ocrProvider = new Infrastructure.OCR.TesseractOCRProvider(tessdataPath);
            var pdfProcessor = new Infrastructure.PDF.PdfPigPDFProcessor();
            var patternMatcher = new PatternMatcher(config);
            var excelExporter = new ExcelExporter();
            
            return new BatchProcessor(ocrProvider, pdfProcessor, patternMatcher, excelExporter, logger, config);
        }

        /// <summary>
        /// Process all PDF files from the input folder (synchronous version for MainWindow compatibility)
        /// </summary>
        public List<ExtractionResult> ProcessFolder(string inputFolder)
        {
            return ProcessFolderAsync(inputFolder, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Process all PDF files from the input folder asynchronously
        /// </summary>
        public async Task<List<ExtractionResult>> ProcessFolderAsync(string inputFolder, CancellationToken? cancellationToken = null)
        {
            // Validate input folder
            if (string.IsNullOrWhiteSpace(inputFolder))
            {
                throw new ArgumentException("Input folder path cannot be empty", nameof(inputFolder));
            }

            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException($"Input folder not found: {inputFolder}");
            }

            // Get PDF files
            var pdfFiles = Directory.GetFiles(inputFolder, "*.pdf");
            _totalFiles = pdfFiles.Length;

            if (_totalFiles == 0)
            {
                OnProcessingCompleted(new ProcessingCompletedEventArgs
                {
                    Success = true,
                    Message = "No PDF files found in the input folder",
                    Results = new List<ExtractionResult>(),
                    Logs = new List<ProcessingLog>()
                });
                return new List<ExtractionResult>();
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
            var results = new List<ExtractionResult>();
            var overallStartTime = DateTime.Now;

            try
            {
                // Process each file
                for (int i = 0; i < pdfFiles.Length; i++)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    var filePath = pdfFiles[i];
                    var fileName = Path.GetFileName(filePath);

                    OnFileProcessing(new FileProcessingEventArgs
                    {
                        FileName = fileName,
                        CurrentFile = i + 1,
                        TotalFiles = _totalFiles,
                        Status = "Processing"
                    });

                    // Raise ProgressUpdated event
                    OnProgressUpdated(new ProgressReport
                    {
                        FileNumber = i + 1,
                        TotalFiles = _totalFiles,
                        CurrentFile = filePath,
                        Status = "Processing",
                        PercentComplete = (int)((float)(i) / _totalFiles * 100)
                    });

                    try
                    {
                        var fileResults = await ProcessSingleFileAsync(filePath);
                        results.AddRange(fileResults);
                        _processedFiles++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(new ProcessingLog
                        {
                            FileName = fileName,
                            PageNumber = 0,
                            Status = "error",
                            Message = $"File processing failed: {ex.Message}"
                        });

                        OnFileProcessing(new FileProcessingEventArgs
                        {
                            FileName = fileName,
                            CurrentFile = i + 1,
                            TotalFiles = _totalFiles,
                            Status = "Error",
                            ErrorMessage = ex.Message
                        });
                    }

                    // Report progress
                    var progress = (float)_processedFiles / _totalFiles * 100;
                    OnProgressChanged(new ProgressChangedEventArgs((int)progress, null));

                    // Raise ProgressUpdated event
                    OnProgressUpdated(new ProgressReport
                    {
                        FileNumber = i + 1,
                        TotalFiles = _totalFiles,
                        CurrentFile = filePath,
                        Status = "Complete",
                        PercentComplete = (int)progress
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                OnProcessingCompleted(new ProcessingCompletedEventArgs
                {
                    Success = false,
                    Message = $"Batch processing failed: {ex.Message}",
                    Results = results,
                    Logs = GetAllLogs(),
                    Error = ex
                });
                throw;
            }
        }

        /// <summary>
        /// Process all PDF files from the input folder
        /// </summary>
        public async Task ProcessBatchAsync(
            string inputFolder,
            string outputFolder,
            CancellationToken cancellationToken = default)
        {
            // Validate input folder
            if (string.IsNullOrWhiteSpace(inputFolder))
            {
                throw new ArgumentException("Input folder path cannot be empty", nameof(inputFolder));
            }

            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException($"Input folder not found: {inputFolder}");
            }

            // Ensure output folder exists
            if (!string.IsNullOrWhiteSpace(outputFolder) && !Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Get PDF files
            var pdfFiles = Directory.GetFiles(inputFolder, "*.pdf");
            _totalFiles = pdfFiles.Length;

            if (_totalFiles == 0)
            {
                OnProcessingCompleted(new ProcessingCompletedEventArgs
                {
                    Success = true,
                    Message = "No PDF files found in the input folder",
                    Results = new List<ExtractionResult>(),
                    Logs = new List<ProcessingLog>()
                });
                return;
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var results = new List<ExtractionResult>();
            var overallStartTime = DateTime.Now;

            try
            {
                // Process each file
                for (int i = 0; i < pdfFiles.Length; i++)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    var filePath = pdfFiles[i];
                    var fileName = Path.GetFileName(filePath);

                    OnFileProcessing(new FileProcessingEventArgs
                    {
                        FileName = fileName,
                        CurrentFile = i + 1,
                        TotalFiles = _totalFiles,
                        Status = "Processing"
                    });

                    try
                    {
                        var fileResults = await ProcessSingleFileAsync(filePath);
                        results.AddRange(fileResults);
                        _processedFiles++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(new ProcessingLog
                        {
                            FileName = fileName,
                            PageNumber = 0,
                            Status = "error",
                            Message = $"File processing failed: {ex.Message}"
                        });

                        OnFileProcessing(new FileProcessingEventArgs
                        {
                            FileName = fileName,
                            CurrentFile = i + 1,
                            TotalFiles = _totalFiles,
                            Status = "Error",
                            ErrorMessage = ex.Message
                        });
                    }

                    // Report progress
                    var progress = (float)_processedFiles / _totalFiles * 100;
                    OnProgressChanged(new ProgressChangedEventArgs((int)progress, null));
                }

                // Python deduplication: keep only one occurrence per (type, value), prefer higher confidence
                var deduplicatedResults = new List<ExtractionResult>();
                var seenTags = new Dictionary<string, (ExtractionResult Result, TagItem Tag)>();
                var seenEquipment = new Dictionary<string, (ExtractionResult Result, EquipmentItem Equipment)>();

                // Collect all unique items with highest confidence
                foreach (var result in results)
                {
                    foreach (var tag in result.Tags)
                    {
                        var key = $"{tag.Type}:{tag.Value}";
                        if (!seenTags.ContainsKey(key) || tag.Confidence > seenTags[key].Tag.Confidence)
                        {
                            seenTags[key] = (result, tag);
                        }
                    }

                    foreach (var equip in result.Equipment)
                    {
                        var key = $"EQUIP:{equip.Value}";
                        if (!seenEquipment.ContainsKey(key) || equip.Confidence > seenEquipment[key].Equipment.Confidence)
                        {
                            seenEquipment[key] = (result, equip);
                        }
                    }
                }

                // Build deduplicated results
                var uniqueResults = new Dictionary<string, ExtractionResult>();
                foreach (var (result, tag) in seenTags.Values)
                {
                    var key = $"{result.SourceFile}:{result.PageNumber}";
                    if (!uniqueResults.ContainsKey(key))
                    {
                        uniqueResults[key] = new ExtractionResult
                        {
                            SourceFile = result.SourceFile,
                            PageNumber = result.PageNumber,
                            Confidence = result.Confidence,
                            RawText = result.RawText
                        };
                    }
                    uniqueResults[key].Tags.Add(tag);
                }

                foreach (var (result, equipment) in seenEquipment.Values)
                {
                    var key = $"{result.SourceFile}:{result.PageNumber}";
                    if (!uniqueResults.ContainsKey(key))
                    {
                        uniqueResults[key] = new ExtractionResult
                        {
                            SourceFile = result.SourceFile,
                            PageNumber = result.PageNumber,
                            Confidence = result.Confidence,
                            RawText = result.RawText
                        };
                    }
                    uniqueResults[key].Equipment.Add(equipment);
                }

                results = uniqueResults.Values.ToList();

                // Export results to Excel
                var outputPath = string.IsNullOrWhiteSpace(outputFolder) 
                    ? Path.Combine(inputFolder, $"ExtractionResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx")
                    : Path.Combine(outputFolder, $"ExtractionResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                _excelExporter.Export(outputPath, results);
                _logger.Log(new ProcessingLog
                {
                    FileName = "Batch Processing",
                    PageNumber = 0,
                    Status = "success",
                    Message = $"Results exported to: {outputPath}"
                });

                var duration = DateTime.Now - overallStartTime;
                OnProcessingCompleted(new ProcessingCompletedEventArgs
                {
                    Success = true,
                    Message = $"Batch processing completed. {results.Count} pages processed in {duration.TotalSeconds:F2} seconds.",
                    Results = results,
                    Logs = GetAllLogs(),
                    OutputPath = outputPath
                });
            }
            catch (Exception ex)
            {
                OnProcessingCompleted(new ProcessingCompletedEventArgs
                {
                    Success = false,
                    Message = $"Batch processing failed: {ex.Message}",
                    Results = results,
                    Logs = GetAllLogs(),
                    Error = ex
                });
            }
        }

        /// <summary>
        /// Process a single PDF file
        /// </summary>
        private async Task<List<ExtractionResult>> ProcessSingleFileAsync(string pdfPath)
        {
            var results = new List<ExtractionResult>();
            var fileName = Path.GetFileName(pdfPath);

            // Extract pages from PDF
            var pages = _pdfProcessor.ExtractPages(pdfPath);
            var fileStartTime = DateTime.Now;

            foreach (var page in pages)
            {
                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    break;
                }

                // Check if we should only process scanned pages
                if (_config.ProcessOnlyScannedPages && page.IsSearchable)
                {
                    // Skip searchable pages but log that we skipped them
                    _logger.LogPage(
                        fileName,
                        page.PageNumber,
                        page.IsSearchable,
                        0,
                        "skipped",
                        "Skipped - text-searchable page (ProcessOnlyScannedPages enabled)");
                    continue;
                }

                _processedPages++;
                var result = await ProcessPageAsync(pdfPath, page);
                results.Add(result);

                // Log page processing
                _logger.LogPage(
                    fileName,
                    page.PageNumber,
                    page.IsSearchable,
                    result.Tags.Count + result.Equipment.Count,
                    "success",
                    $"Page processed in {(DateTime.Now - fileStartTime).TotalMilliseconds:F0}ms");

                // Report page progress
                var pageProgress = (float)_processedPages / _totalPages * 100;
                OnProgressChanged(new ProgressChangedEventArgs((int)pageProgress, null));
            }

            return results;
        }

        /// <summary>
        /// Process a single page
        /// </summary>
        private async Task<ExtractionResult> ProcessPageAsync(string pdfPath, PageResult page)
        {
            var result = new ExtractionResult
            {
                SourceFile = pdfPath,
                PageNumber = page.PageNumber,
                IsSearchable = page.IsSearchable,
                RawText = page.RawText
            };

            // DEBUG: Log what we're processing
            System.Diagnostics.Debug.WriteLine($"[BATCH] Processing page {page.PageNumber}: IsSearchable={page.IsSearchable}, TextLength={page.TextLength}");

            // Python's approach: Always use OCR on extracted images
            // This ensures we get the same results as Python
            if (page.ImageData != null && page.ImageData.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BATCH] Page {page.PageNumber}: Using OCR on image data ({page.ImageData.Length} bytes)");
                var ocrResult = await Task.Run(() => _ocrProvider.ProcessImage(page.ImageData));
                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;

                // DEBUG: Show OCR text being processed
                if (!string.IsNullOrWhiteSpace(ocrResult.RawText))
                {
                    var preview = ocrResult.RawText.Length > 200 ? ocrResult.RawText.Substring(0, 200) + "..." : ocrResult.RawText;
                    System.Diagnostics.Debug.WriteLine($"[BATCH] OCR RESULT: {preview.Replace("\n", " ").Replace("\r", "")}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[BATCH] OCR RESULT: (empty)");
                }

                // Apply pattern matching to OCR result
                var patternResult = _patternMatcher.Match(ocrResult.RawText);
                
                // DEBUG: Log OCR pattern matching results
                System.Diagnostics.Debug.WriteLine($"[BATCH] OCR Pattern matches: {patternResult.Tags.Count} tags, {patternResult.Equipment.Count} equipment");
                
                result.Tags.AddRange(patternResult.Tags.ConvertAll(t => new TagItem { Value = t.Value, Type = t.Type, Confidence = t.Confidence }));
                result.Equipment.AddRange(patternResult.Equipment.ConvertAll(e => new EquipmentItem { Value = e.Value, Confidence = e.Confidence }));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[BATCH] Page {page.PageNumber}: No image data available for OCR");
                result.Confidence = 0.0;
            }

            return result;
        }

        /// <summary>
        /// Process a single file synchronously (for testing)
        /// </summary>
        public List<ExtractionResult> ProcessSingleFile(string pdfPath)
        {
            var pages = _pdfProcessor.ExtractPages(pdfPath);
            var results = new List<ExtractionResult>();

            foreach (var page in pages)
            {
                results.Add(Task.Run(() => ProcessPageAsync(pdfPath, page)).Result);
            }

            return results;
        }

        /// <summary>
        /// Cancel the current batch processing
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Get all processing logs
        /// </summary>
        public List<ProcessingLog> GetAllLogs()
        {
            return new List<ProcessingLog>(); // Logger maintains its own collection
        }

        /// <summary>
        /// Set the total page count (optional, for more accurate progress reporting)
        /// </summary>
        public void SetTotalPages(int totalPages)
        {
            _totalPages = totalPages;
        }

        /// <summary>
        /// Raise ProgressChanged event
        /// </summary>
        protected virtual void OnProgressChanged(ProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raise ProcessingCompleted event
        /// </summary>
        protected virtual void OnProcessingCompleted(ProcessingCompletedEventArgs e)
        {
            ProcessingCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Raise FileProcessing event
        /// </summary>
        protected virtual void OnFileProcessing(FileProcessingEventArgs e)
        {
            FileProcessing?.Invoke(this, e);
        }

        /// <summary>
        /// Raise ProgressUpdated event
        /// </summary>
        protected virtual void OnProgressUpdated(ProgressReport e)
        {
            ProgressUpdated?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for processing completed
    /// </summary>
    public class ProcessingCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ExtractionResult> Results { get; set; } = new();
        public List<ProcessingLog> Logs { get; set; } = new();
        public string? OutputPath { get; set; }
        public Exception? Error { get; set; }
    }

    /// <summary>
    /// Event arguments for file processing updates
    /// </summary>
    public class FileProcessingEventArgs : EventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
