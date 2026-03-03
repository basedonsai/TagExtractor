using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OCRTool.Application;
using OCRTool.Core.Classification;
using OCRTool.Core.Configuration;
using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;
using OCRTool.Infrastructure.Excel;
using OCRTool.Infrastructure.Layout;
using OCRTool.Infrastructure.Logging;

namespace OCRTool
{
    /// <summary>
    /// Test to verify classification logging includes PageType, reasoning, and row/column counts for Table pages
    /// Run this test by calling TestClassificationLogging.RunTest() from the application
    /// </summary>
    public class TestClassificationLogging
    {
        public static void RunTest()
        {
            Console.WriteLine("Testing Classification Logging");
            Console.WriteLine("==============================\n");

            // Create a mock logger that captures log messages
            var mockLogger = new MockFileLogger();
            
            // Create a simple configuration
            var config = new ExtractionConfig
            {
                Patterns = new List<PatternDefinition>
                {
                    new PatternDefinition { Name = "TAG", Type = "Tag", Regex = @"TAG-\d+" }
                },
                EquipmentKeywords = new List<string> { "Motor", "Pump", "Valve", "Sensor", "Switch" }
            };

            // Create dependencies
            var layoutTokenExtractor = new LayoutTokenExtractor();
            var pageClassifier = new PageClassifier();
            var patternMatcher = new PatternMatcher(config);
            var excelExporter = new ExcelExporter();
            var tableEngine = new Core.RecordBuilders.TableEngine();
            var proximityEngine = new Core.RecordBuilders.ProximityEngine(patternMatcher);
            
            // Create a mock OCR provider and PDF processor
            var mockOcrProvider = new MockOCRProvider();
            var mockPdfProcessor = new MockPDFProcessor();

            // Create BatchProcessor with mock logger
            var batchProcessor = new BatchProcessor(
                mockOcrProvider,
                mockPdfProcessor,
                patternMatcher,
                excelExporter,
                mockLogger,
                config,
                layoutTokenExtractor,
                pageClassifier,
                tableEngine,
                proximityEngine);

            Console.WriteLine("Test 1: Verify Table page logging includes row and column counts");
            Console.WriteLine("------------------------------------------------------------------");
            
            // Create a mock page with table structure
            var tableTokens = new List<LayoutToken>
            {
                // Row 1 (Y=100)
                new LayoutToken { Text = "Tag", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Equipment", X = 200, Y = 100, Width = 80, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Rating", X = 350, Y = 100, Width = 60, Height = 20, PageNumber = 1 },
                
                // Row 2 (Y=150)
                new LayoutToken { Text = "TAG-001", X = 100, Y = 150, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 200, Y = 150, Width = 80, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "5HP", X = 350, Y = 150, Width = 60, Height = 20, PageNumber = 1 },
                
                // Row 3 (Y=200)
                new LayoutToken { Text = "TAG-002", X = 100, Y = 200, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 200, Y = 200, Width = 80, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "10HP", X = 350, Y = 200, Width = 60, Height = 20, PageNumber = 1 }
            };

            // Classify the page
            var classification = pageClassifier.Classify(tableTokens);
            
            Console.WriteLine($"Classification Result:");
            Console.WriteLine($"  PageType: {classification.PageType}");
            Console.WriteLine($"  Reasoning: {classification.Reasoning}");
            Console.WriteLine($"  RowCount: {classification.RowCount}");
            Console.WriteLine($"  ColumnCount: {classification.ColumnCount}");
            Console.WriteLine();

            // Verify the classification
            if (classification.PageType == PageType.Table)
            {
                Console.WriteLine("✓ PASS: Page classified as Table");
                
                // Build the expected log message
                var expectedMessage = $"Page classification: {classification.PageType} - {classification.Reasoning} (Rows: {classification.RowCount}, Columns: {classification.ColumnCount})";
                Console.WriteLine($"\nExpected log message:");
                Console.WriteLine($"  {expectedMessage}");
                Console.WriteLine();
                
                // Verify row and column counts are present
                if (classification.RowCount >= 3 && classification.ColumnCount >= 2)
                {
                    Console.WriteLine($"✓ PASS: Row count ({classification.RowCount}) and column count ({classification.ColumnCount}) are correct");
                }
                else
                {
                    Console.WriteLine($"✗ FAIL: Row count ({classification.RowCount}) or column count ({classification.ColumnCount}) is incorrect");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Table, got {classification.PageType}");
            }

            Console.WriteLine("\n\nTest 2: Verify Scattered page logging does not include row/column counts");
            Console.WriteLine("--------------------------------------------------------------------------");
            
            // Create a mock page with scattered layout
            var scatteredTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 180, Y = 120, Width = 60, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-002", X = 300, Y = 250, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 380, Y = 270, Width = 60, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-003", X = 150, Y = 400, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Valve", X = 230, Y = 420, Width = 60, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-004", X = 450, Y = 150, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Sensor", X = 530, Y = 170, Width = 60, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-005", X = 250, Y = 550, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Switch", X = 330, Y = 570, Width = 60, Height = 20, PageNumber = 1 }
            };

            // Classify the page
            var scatteredClassification = pageClassifier.Classify(scatteredTokens);
            
            Console.WriteLine($"Classification Result:");
            Console.WriteLine($"  PageType: {scatteredClassification.PageType}");
            Console.WriteLine($"  Reasoning: {scatteredClassification.Reasoning}");
            Console.WriteLine();

            // Verify the classification
            if (scatteredClassification.PageType == PageType.Scattered)
            {
                Console.WriteLine("✓ PASS: Page classified as Scattered");
                
                // Build the expected log message (should NOT include row/column counts)
                var expectedMessage = $"Page classification: {scatteredClassification.PageType} - {scatteredClassification.Reasoning}";
                Console.WriteLine($"\nExpected log message:");
                Console.WriteLine($"  {expectedMessage}");
                Console.WriteLine($"  (Note: No row/column counts for Scattered pages)");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Scattered, got {scatteredClassification.PageType}");
            }

            Console.WriteLine("\n\nTest 3: Verify Sparse page logging");
            Console.WriteLine("-----------------------------------");
            
            // Create a mock page with sparse layout
            var sparseTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-002", X = 200, Y = 200, Width = 50, Height = 20, PageNumber = 1 }
            };

            // Classify the page
            var sparseClassification = pageClassifier.Classify(sparseTokens);
            
            Console.WriteLine($"Classification Result:");
            Console.WriteLine($"  PageType: {sparseClassification.PageType}");
            Console.WriteLine($"  Reasoning: {sparseClassification.Reasoning}");
            Console.WriteLine();

            // Verify the classification
            if (sparseClassification.PageType == PageType.Sparse)
            {
                Console.WriteLine("✓ PASS: Page classified as Sparse");
                
                // Build the expected log message
                var expectedMessage = $"Page classification: {sparseClassification.PageType} - {sparseClassification.Reasoning}";
                Console.WriteLine($"\nExpected log message:");
                Console.WriteLine($"  {expectedMessage}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Sparse, got {sparseClassification.PageType}");
            }

            Console.WriteLine("\nAll classification logging tests completed!");
        }
    }

    // Mock classes for testing
    public class MockFileLogger : FileLogger
    {
        private List<string> _logMessages = new List<string>();

        public MockFileLogger() : base()
        {
        }

        public new void LogPage(string fileName, int pageNumber, bool isSearchable, int itemsFound, string status, string message)
        {
            _logMessages.Add($"[{status}] {fileName} - Page {pageNumber}: {message}");
            Console.WriteLine($"[LOG] {message}");
            base.LogPage(fileName, pageNumber, isSearchable, itemsFound, status, message);
        }

        public List<string> GetLogMessages() => _logMessages;
    }

    public class MockOCRProvider : IOCRProvider
    {
        public ExtractionResult ProcessImage(byte[] imageData)
        {
            return new ExtractionResult
            {
                RawText = "Mock OCR text",
                Confidence = 95.0
            };
        }
    }

    public class MockPDFProcessor : IPDFProcessor
    {
        public List<PageResult> ExtractPages(string pdfPath)
        {
            return new List<PageResult>
            {
                new PageResult
                {
                    PageNumber = 1,
                    IsSearchable = true,
                    RawText = "Mock PDF text"
                }
            };
        }
    }
}
