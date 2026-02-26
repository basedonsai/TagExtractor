using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OCRTool.Core.Configuration;
using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;
using OCRTool.Infrastructure.Excel;
using OCRTool.Infrastructure.Logging;

namespace OCRTool.Application
{
      
    /// Main orchestration class that processes PDF files using OCR providers and PDF processors.
    /// Handles batch processing, progress reporting, cancellation, and result export.
    
    public class BatchProcessor
    {
        private readonly IOCRProvider _ocrProvider;
        private readonly IPDFProcessor _pdfProcessor;
        private readonly PatternMatcher _patternMatcher;
        private readonly ExcelExporter _excelExporter;
        private readonly FileLogger _logger;
        private readonly ExtractionConfig _config;
        private readonly List<ProcessingLog> _allLogs = new List<ProcessingLog>();
        
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalFiles;
        private int _processedFiles;
        private int _totalPages;
        private int _processedPages;

          
        /// Event raised when progress changes
        
        public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

          
        /// Event raised when processing completes
        
        public event EventHandler<ProcessingCompletedEventArgs>? ProcessingCompleted;

       
        /// Event raised for individual file processing updates
        
        public event EventHandler<FileProcessingEventArgs>? FileProcessing;

        
        /// Creates a new BatchProcessor with the specified dependencies
        
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

        
        /// Creates a BatchProcessor with default implementations
        
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

        
        /// Process all PDF files from the input folder
        
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

            // Get PDF files from the input folder
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

            await ProcessFilesAsync(pdfFiles, outputFolder);
        }

        
        /// Process specific PDF files (instead of scanning a folder)
        
        public async Task ProcessSpecificFilesAsync(
            List<string> filePaths,
            string outputFolder,
            CancellationToken cancellationToken = default)
        {
            if (filePaths == null || filePaths.Count == 0)
            {
                throw new ArgumentException("File list cannot be empty", nameof(filePaths));
            }

            // Validate all files exist
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }
            }

            // Ensure output folder exists
            if (!string.IsNullOrWhiteSpace(outputFolder) && !Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            _totalFiles = filePaths.Count;

            await ProcessFilesAsync(filePaths.ToArray(), outputFolder);
        }

        
        /// Internal method to process a list of files
        
        private async Task ProcessFilesAsync(string[] pdfFiles, string outputFolder)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var results = new List<ExtractionResult>();
            var overallStartTime = DateTime.Now;
            var inputFolder = Path.GetDirectoryName(pdfFiles[0]) ?? string.Empty;

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
                        var logEntry = new ProcessingLog
                        {
                            FileName = fileName,
                            PageNumber = 0,
                            Status = "error",
                            Message = $"File processing failed: {ex.Message}"
                        };
                        _allLogs.Add(logEntry);
                        _logger.Log(logEntry);

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

                // Export results to Excel
                var outputPath = string.IsNullOrWhiteSpace(outputFolder) 
                    ? Path.Combine(inputFolder, $"ExtractionResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx")
                    : Path.Combine(outputFolder, $"ExtractionResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                _excelExporter.Export(outputPath, results, _allLogs);
                var exportLogEntry = new ProcessingLog
                {
                    FileName = "Batch Processing",
                    PageNumber = 0,
                    Status = "success",
                    Message = $"Results exported to: {outputPath}"
                };
                _allLogs.Add(exportLogEntry);
                _logger.Log(exportLogEntry);

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

        
        /// Process a single PDF file
        
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

                // Process ALL pages:
                // - Text-searchable pages: extract text directly (fast, no OCR)
                // - Scanned pages: perform OCR (heavy), then extract text
                // Both types are validated through pattern matching (regex)
                _processedPages++;
                var result = await ProcessPageAsync(pdfPath, page);
                results.Add(result);

                // Log page processing
                var pageType = page.IsSearchable ? "text-searchable" : "scanned";
                var extractionMethod = page.IsSearchable ? "direct extraction" : "OCR";
                var pageLogEntry = new ProcessingLog
                {
                    FileName = fileName,
                    PageNumber = page.PageNumber,
                    PageType = page.IsSearchable ? "searchable" : "scanned",
                    ItemsFound = result.Tags.Count + result.Equipment.Count,
                    Status = "success",
                    Message = $"Page processed ({pageType}) using {extractionMethod} in {(DateTime.Now - fileStartTime).TotalMilliseconds:F0}ms"
                };
                _allLogs.Add(pageLogEntry);
                _logger.LogPage(
                    fileName,
                    page.PageNumber,
                    page.IsSearchable,
                    result.Tags.Count + result.Equipment.Count,
                    "success",
                    $"Page processed ({pageType}) using {extractionMethod} in {(DateTime.Now - fileStartTime).TotalMilliseconds:F0}ms");

                // Report page progress
                var pageProgress = (float)_processedPages / _totalPages * 100;
                OnProgressChanged(new ProgressChangedEventArgs((int)pageProgress, null));
            }

            return results;
        }

        
        /// Process a single page
        
        private async Task<ExtractionResult> ProcessPageAsync(string pdfPath, PageResult page)
        {
            var result = new ExtractionResult
            {
                SourceFile = pdfPath,
                PageNumber = page.PageNumber,
                IsSearchable = page.IsSearchable,
                RawText = page.RawText
            };

            // Debug: Log text extraction info for ALL pages
            var rawText = page.RawText ?? string.Empty;
            var textLength = rawText.Length;

            var textPreview = textLength > 0
                ? (rawText.Length > 100
                    ? rawText.Substring(0, 100).Replace("\n", " ").Replace("\r", "") + "..."
                    : rawText.Replace("\n", " ").Replace("\r", ""))
                : "(empty)";

            _logger.LogPage(
                Path.GetFileName(pdfPath),
                page.PageNumber,
                page.IsSearchable,
                0,
                "debug",
                $"Page {page.PageNumber}: IsSearchable={page.IsSearchable}, TextLength={textLength}, HasImage={page.ImageData?.Length > 0}, Text='{textPreview}'");

            // =====================================================
            // HYBRID EXTRACTION LOGIC (CORRECT APPROACH)
            // =====================================================

            string pageText = page.RawText ?? string.Empty;
            double confidence = 100.0; // searchable text assumed high confidence

            // If searchable text is empty, fallback to OCR
            if (string.IsNullOrWhiteSpace(pageText) && page.ImageData?.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BATCH] Page {page.PageNumber}: No searchable text found. Using OCR.");

                var ocrResult = await Task.Run(() => _ocrProvider.ProcessImage(page.ImageData));

                pageText = ocrResult.RawText ?? string.Empty;
                confidence = ocrResult.Confidence;

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    var preview = pageText.Length > 200
                        ? pageText.Substring(0, 200) + "..."
                        : pageText;

                    System.Diagnostics.Debug.WriteLine(
                        $"[BATCH] OCR RESULT: {preview.Replace("\n", " ").Replace("\r", "")}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[BATCH] OCR RESULT: (empty)");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BATCH] Page {page.PageNumber}: Using searchable text directly.");
            }

            // Update result text + confidence
            result.RawText = pageText;
            result.Confidence = confidence;

            // =====================================================
            // PATTERN MATCHING
            // =====================================================

            var normalizedText = pageText
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("  ", " ");

            var patternResult = _patternMatcher.Match(
                normalizedText,
                Path.GetFileName(pdfPath),
                page.PageNumber,
                confidence
            );

            System.Diagnostics.Debug.WriteLine(
                $"[BATCH] Pattern matches: {patternResult.Tags.Count} tags, {patternResult.Equipment.Count} equipment");

            result.Tags.AddRange(patternResult.Tags);
            result.Equipment.AddRange(patternResult.Equipment);

            return result;
        }


        
        /// Process a single file synchronously (for testing)
        
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

        
        /// Cancel the current batch processing
        
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        
        /// Get all processing logs
        
        public List<ProcessingLog> GetAllLogs()
        {
            return _allLogs;
        }

        
        /// Set the total page count (optional, for more accurate progress reporting)
        
        public void SetTotalPages(int totalPages)
        {
            _totalPages = totalPages;
        }

        
        /// Raise ProgressChanged event
        
        protected virtual void OnProgressChanged(ProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        
        /// Raise ProcessingCompleted event
        
        protected virtual void OnProcessingCompleted(ProcessingCompletedEventArgs e)
        {
            ProcessingCompleted?.Invoke(this, e);
        }

        
        /// Raise FileProcessing event
        
        protected virtual void OnFileProcessing(FileProcessingEventArgs e)
        {
            FileProcessing?.Invoke(this, e);
        }
    }

    
    /// Event arguments for processing completed
    
    public class ProcessingCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ExtractionResult> Results { get; set; } = new();
        public List<ProcessingLog> Logs { get; set; } = new();
        public string? OutputPath { get; set; }
        public Exception? Error { get; set; }
    }

    
    /// Event arguments for file processing updates
    
    public class FileProcessingEventArgs : EventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
