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
        private readonly ILayoutTokenExtractor _layoutTokenExtractor;
        private readonly IPageClassifier _pageClassifier;
        private readonly IRecordBuilder _tableEngine;
        private readonly IRecordBuilder _proximityEngine;
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
            ExtractionConfig config,
            ILayoutTokenExtractor layoutTokenExtractor,
            IPageClassifier pageClassifier,
            IRecordBuilder tableEngine,
            IRecordBuilder proximityEngine)
        {
            _ocrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
            _pdfProcessor = pdfProcessor ?? throw new ArgumentNullException(nameof(pdfProcessor));
            _patternMatcher = patternMatcher ?? throw new ArgumentNullException(nameof(patternMatcher));
            _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _layoutTokenExtractor = layoutTokenExtractor ?? throw new ArgumentNullException(nameof(layoutTokenExtractor));
            _pageClassifier = pageClassifier ?? throw new ArgumentNullException(nameof(pageClassifier));
            _tableEngine = tableEngine ?? throw new ArgumentNullException(nameof(tableEngine));
            _proximityEngine = proximityEngine ?? throw new ArgumentNullException(nameof(proximityEngine));
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
            var layoutTokenExtractor = new Infrastructure.Layout.LayoutTokenExtractor();
            var pageClassifier = new Core.Classification.PageClassifier();
            var tableEngine = new Core.RecordBuilders.TableEngine();
            var proximityEngine = new Core.RecordBuilders.ProximityEngine(patternMatcher);
            
            return new BatchProcessor(ocrProvider, pdfProcessor, patternMatcher, excelExporter, logger, config, layoutTokenExtractor, pageClassifier, tableEngine, proximityEngine);
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
            // LAYOUT TOKEN EXTRACTION
            // =====================================================

            try
            {
                var extractionStartTime = DateTime.Now;
                
                if (page.IsSearchable && page.PdfPigPage != null)
                {
                    // Extract layout tokens from searchable PDF using PdfPig
                    result.LayoutTokens = _layoutTokenExtractor.ExtractFromPdfPigPage(page.PdfPigPage, page.PageNumber);
                    
                    var extractionTime = (DateTime.Now - extractionStartTime).TotalMilliseconds;
                    _logger.LogPage(
                        Path.GetFileName(pdfPath),
                        page.PageNumber,
                        page.IsSearchable,
                        0,
                        "info",
                        $"Layout token extraction: {result.LayoutTokens.Count} tokens from PdfPig in {extractionTime:F0}ms");
                    
                    System.Diagnostics.Debug.WriteLine(
                        $"[BATCH] Page {page.PageNumber}: Extracted {result.LayoutTokens.Count} layout tokens from PdfPig in {extractionTime:F0}ms");
                }
                else if (!page.IsSearchable && page.ImageData?.Length > 0)
                {
                    // Extract layout tokens from scanned page using Tesseract TSV
                    var tsvOutput = (_ocrProvider as Infrastructure.OCR.TesseractOCRProvider)?.GetTsvOutput(page.ImageData);
                    
                    if (!string.IsNullOrWhiteSpace(tsvOutput))
                    {
                        result.LayoutTokens = _layoutTokenExtractor.ExtractFromTesseractTsv(tsvOutput, page.PageNumber);
                        
                        var extractionTime = (DateTime.Now - extractionStartTime).TotalMilliseconds;
                        _logger.LogPage(
                            Path.GetFileName(pdfPath),
                            page.PageNumber,
                            page.IsSearchable,
                            0,
                            "info",
                            $"Layout token extraction: {result.LayoutTokens.Count} tokens from Tesseract TSV in {extractionTime:F0}ms");
                        
                        System.Diagnostics.Debug.WriteLine(
                            $"[BATCH] Page {page.PageNumber}: Extracted {result.LayoutTokens.Count} layout tokens from Tesseract TSV in {extractionTime:F0}ms");
                    }
                    else
                    {
                        result.LayoutTokens = new List<LayoutToken>(); // Empty collection on TSV failure
                        _logger.LogPage(
                            Path.GetFileName(pdfPath),
                            page.PageNumber,
                            page.IsSearchable,
                            0,
                            "warning",
                            "Layout token extraction: Failed to get Tesseract TSV output, continuing with text-only extraction");
                        
                        System.Diagnostics.Debug.WriteLine(
                            $"[BATCH] Page {page.PageNumber}: Failed to get Tesseract TSV output for layout token extraction");
                    }
                }
                else
                {
                    // No layout token extraction possible (empty page or missing data)
                    result.LayoutTokens = new List<LayoutToken>();
                    _logger.LogPage(
                        Path.GetFileName(pdfPath),
                        page.PageNumber,
                        page.IsSearchable,
                        0,
                        "info",
                        "Layout token extraction: Skipped (no searchable text or image data available)");
                }
            }
            catch (Exception ex)
            {
                // Graceful fallback: log error and continue with empty layout token collection
                result.LayoutTokens = new List<LayoutToken>();
                _logger.LogPage(
                    Path.GetFileName(pdfPath),
                    page.PageNumber,
                    page.IsSearchable,
                    0,
                    "error",
                    $"Layout token extraction failed: {ex.Message}. Continuing with text-only extraction.");
                
                System.Diagnostics.Debug.WriteLine($"[BATCH] Layout token extraction failed for page {page.PageNumber}: {ex.Message}");
            }

            // =====================================================
            // PAGE CLASSIFICATION
            // =====================================================

            try
            {
                // Classify page based on layout token distribution
                result.Classification = _pageClassifier.Classify(result.LayoutTokens);
                
                // Build classification log message with row/column counts for Table pages
                var classificationMessage = $"Page classification: {result.Classification.PageType} - {result.Classification.Reasoning}";
                if (result.Classification.PageType == PageType.Table)
                {
                    classificationMessage += $" (Rows: {result.Classification.RowCount}, Columns: {result.Classification.ColumnCount})";
                }
                
                _logger.LogPage(
                    Path.GetFileName(pdfPath),
                    page.PageNumber,
                    page.IsSearchable,
                    0,
                    "info",
                    classificationMessage);
                
                System.Diagnostics.Debug.WriteLine(
                    $"[BATCH] Page {page.PageNumber}: {classificationMessage}");
            }
            catch (Exception ex)
            {
                // Graceful fallback: default to Sparse and log error
                result.Classification = new PageClassification
                {
                    PageType = PageType.Sparse,
                    Reasoning = "Classification failed, defaulted to Sparse",
                    RowCount = 0,
                    ColumnCount = 0
                };
                
                _logger.LogPage(
                    Path.GetFileName(pdfPath),
                    page.PageNumber,
                    page.IsSearchable,
                    0,
                    "error",
                    $"Page classification failed: {ex.Message}. Defaulted to Sparse.");
                
                System.Diagnostics.Debug.WriteLine($"[BATCH] Page classification failed for page {page.PageNumber}: {ex.Message}");
            }

            // =====================================================
            // PAGE ROUTING AND STRUCTURED RECORD BUILDING
            // =====================================================

            try
            {
                // Route page to appropriate extraction engine based on classification
                if (result.Classification != null)
                {
                    var pageType = result.Classification.PageType;
                    var sourceFileName = Path.GetFileName(pdfPath);

                    if (pageType == PageType.Table)
                    {
                        // Invoke TableEngine for table-like layouts
                        result.StructuredRecords = _tableEngine.BuildRecords(
                            result.LayoutTokens,
                            page.PageNumber,
                            sourceFileName);

                        _logger.LogPage(
                            sourceFileName,
                            page.PageNumber,
                            page.IsSearchable,
                            0,
                            "info",
                            $"Structured record building: TableEngine produced {result.StructuredRecords.Count} records");

                        System.Diagnostics.Debug.WriteLine(
                            $"[BATCH] Page {page.PageNumber}: TableEngine produced {result.StructuredRecords.Count} records");
                    }
                    else if (pageType == PageType.Scattered)
                    {
                        // Invoke ProximityEngine for scattered layouts
                        result.StructuredRecords = _proximityEngine.BuildRecords(
                            result.LayoutTokens,
                            page.PageNumber,
                            sourceFileName);

                        _logger.LogPage(
                            sourceFileName,
                            page.PageNumber,
                            page.IsSearchable,
                            0,
                            "info",
                            $"Structured record building: ProximityEngine produced {result.StructuredRecords.Count} records");

                        System.Diagnostics.Debug.WriteLine(
                            $"[BATCH] Page {page.PageNumber}: ProximityEngine produced {result.StructuredRecords.Count} records");
                    }
                    else if (pageType == PageType.Sparse)
                    {
                        // Skip structured record building for sparse pages
                        result.StructuredRecords = new List<StructuredRecord>();

                        _logger.LogPage(
                            sourceFileName,
                            page.PageNumber,
                            page.IsSearchable,
                            0,
                            "info",
                            "Structured record building: Skipped (Sparse page)");

                        System.Diagnostics.Debug.WriteLine(
                            $"[BATCH] Page {page.PageNumber}: Skipped structured record building (Sparse page)");
                    }
                }
                else
                {
                    // No classification available, skip structured record building
                    result.StructuredRecords = new List<StructuredRecord>();
                }
            }
            catch (Exception ex)
            {
                // Graceful fallback: log error and continue with empty structured records
                result.StructuredRecords = new List<StructuredRecord>();
                _logger.LogPage(
                    Path.GetFileName(pdfPath),
                    page.PageNumber,
                    page.IsSearchable,
                    0,
                    "error",
                    $"Structured record building failed: {ex.Message}. Continuing with regex-only extraction.");

                System.Diagnostics.Debug.WriteLine($"[BATCH] Structured record building failed for page {page.PageNumber}: {ex.Message}");
            }

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
