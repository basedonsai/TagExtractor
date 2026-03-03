using System;
using System.Collections.Generic;
using OCRTool.Core.Classification;
using OCRTool.Core.Models;

namespace OCRTool
{
    /// <summary>
    /// Simple manual test for PageClassifier
    /// Run these tests by calling TestPageClassifier.RunAllTests() from the application
    /// </summary>
    public class TestPageClassifier
    {
        public static void RunAllTests()
        {
            Console.WriteLine("Testing PageClassifier.Classify");
            Console.WriteLine("================================\n");

            var classifier = new PageClassifier();

            // Test 1: Null token collection returns Sparse
            Console.WriteLine("Test 1: Null token collection returns Sparse");
            var result1 = classifier.Classify(null!);
            if (result1.PageType == PageType.Sparse && result1.Reasoning.Contains("No tokens"))
            {
                Console.WriteLine($"✓ PASS: Null collection classified as Sparse");
                Console.WriteLine($"  Reasoning: {result1.Reasoning}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Sparse, got {result1.PageType}\n");
            }

            // Test 2: Empty token collection returns Sparse
            Console.WriteLine("Test 2: Empty token collection returns Sparse");
            var result2 = classifier.Classify(new List<LayoutToken>());
            if (result2.PageType == PageType.Sparse && result2.Reasoning.Contains("No tokens"))
            {
                Console.WriteLine($"✓ PASS: Empty collection classified as Sparse");
                Console.WriteLine($"  Reasoning: {result2.Reasoning}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Sparse, got {result2.PageType}\n");
            }

            // Test 3: Fewer than 10 tokens returns Sparse
            Console.WriteLine("Test 3: Fewer than 10 tokens returns Sparse");
            var sparseTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-002", X = 200, Y = 200, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "TAG-003", X = 300, Y = 300, Width = 50, Height = 20, PageNumber = 1 }
            };
            var result3 = classifier.Classify(sparseTokens);
            if (result3.PageType == PageType.Sparse && result3.Reasoning.Contains("below threshold"))
            {
                Console.WriteLine($"✓ PASS: 3 tokens classified as Sparse");
                Console.WriteLine($"  Reasoning: {result3.Reasoning}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Sparse, got {result3.PageType}\n");
            }

            // Test 4: Table structure (3 rows, 3 columns) returns Table
            Console.WriteLine("Test 4: Table structure (3 rows, 3 columns) returns Table");
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
                new LayoutToken { Text = "10HP", X = 350, Y = 200, Width = 60, Height = 20, PageNumber = 1 },
                
                // Row 4 (Y=250)
                new LayoutToken { Text = "TAG-003", X = 100, Y = 250, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Valve", X = 200, Y = 250, Width = 80, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "2in", X = 350, Y = 250, Width = 60, Height = 20, PageNumber = 1 }
            };
            var result4 = classifier.Classify(tableTokens);
            if (result4.PageType == PageType.Table && result4.RowCount >= 3 && result4.ColumnCount >= 2)
            {
                Console.WriteLine($"✓ PASS: Table structure detected");
                Console.WriteLine($"  Reasoning: {result4.Reasoning}");
                Console.WriteLine($"  Rows: {result4.RowCount}, Columns: {result4.ColumnCount}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Table, got {result4.PageType}");
                Console.WriteLine($"  Rows: {result4.RowCount}, Columns: {result4.ColumnCount}\n");
            }

            // Test 5: Scattered layout (10+ tokens, no table structure) returns Scattered
            Console.WriteLine("Test 5: Scattered layout (10+ tokens, no table structure) returns Scattered");
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
            var result5 = classifier.Classify(scatteredTokens);
            if (result5.PageType == PageType.Scattered && result5.Reasoning.Contains("Irregular"))
            {
                Console.WriteLine($"✓ PASS: Scattered layout detected");
                Console.WriteLine($"  Reasoning: {result5.Reasoning}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Scattered, got {result5.PageType}");
                Console.WriteLine($"  Reasoning: {result5.Reasoning}\n");
            }

            // Test 6: HasTableStructure method directly
            Console.WriteLine("Test 6: HasTableStructure method with table layout");
            bool hasTable = classifier.HasTableStructure(tableTokens, out int rowCount, out int columnCount);
            if (hasTable && rowCount >= 3 && columnCount >= 2)
            {
                Console.WriteLine($"✓ PASS: HasTableStructure correctly detected table");
                Console.WriteLine($"  Rows: {rowCount}, Columns: {columnCount}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: HasTableStructure failed");
                Console.WriteLine($"  HasTable: {hasTable}, Rows: {rowCount}, Columns: {columnCount}\n");
            }

            // Test 7: HasTableStructure with scattered layout
            Console.WriteLine("Test 7: HasTableStructure method with scattered layout");
            bool hasTable2 = classifier.HasTableStructure(scatteredTokens, out int rowCount2, out int columnCount2);
            if (!hasTable2)
            {
                Console.WriteLine($"✓ PASS: HasTableStructure correctly rejected scattered layout");
                Console.WriteLine($"  Rows: {rowCount2}, Columns: {columnCount2}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: HasTableStructure incorrectly detected table in scattered layout");
                Console.WriteLine($"  Rows: {rowCount2}, Columns: {columnCount2}\n");
            }

            // Test 8: Edge case - exactly 10 tokens, no table structure
            Console.WriteLine("Test 8: Edge case - exactly 10 tokens, no table structure");
            var edgeTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "T1", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T2", X = 180, Y = 120, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T3", X = 300, Y = 250, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T4", X = 380, Y = 270, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T5", X = 150, Y = 400, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T6", X = 230, Y = 420, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T7", X = 450, Y = 150, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T8", X = 530, Y = 170, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T9", X = 250, Y = 550, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "T10", X = 330, Y = 570, Width = 50, Height = 20, PageNumber = 1 }
            };
            var result8 = classifier.Classify(edgeTokens);
            if (result8.PageType == PageType.Scattered)
            {
                Console.WriteLine($"✓ PASS: 10 tokens without table structure classified as Scattered");
                Console.WriteLine($"  Reasoning: {result8.Reasoning}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Scattered, got {result8.PageType}");
                Console.WriteLine($"  Reasoning: {result8.Reasoning}\n");
            }

            // Test 9: Edge case - exactly 10 tokens with table structure
            Console.WriteLine("Test 9: Edge case - exactly 10 tokens with table structure");
            var edgeTableTokens = new List<LayoutToken>
            {
                // Row 1 (Y=100)
                new LayoutToken { Text = "Tag", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Equipment", X = 200, Y = 100, Width = 80, Height = 20, PageNumber = 1 },
                
                // Row 2 (Y=150)
                new LayoutToken { Text = "TAG-001", X = 100, Y = 150, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 200, Y = 150, Width = 80, Height = 20, PageNumber = 1 },
                
                // Row 3 (Y=200)
                new LayoutToken { Text = "TAG-002", X = 100, Y = 200, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 200, Y = 200, Width = 80, Height = 20, PageNumber = 1 },
                
                // Row 4 (Y=250)
                new LayoutToken { Text = "TAG-003", X = 100, Y = 250, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Valve", X = 200, Y = 250, Width = 80, Height = 20, PageNumber = 1 },
                
                // Row 5 (Y=300)
                new LayoutToken { Text = "TAG-004", X = 100, Y = 300, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Sensor", X = 200, Y = 300, Width = 80, Height = 20, PageNumber = 1 }
            };
            var result9 = classifier.Classify(edgeTableTokens);
            if (result9.PageType == PageType.Table)
            {
                Console.WriteLine($"✓ PASS: 10 tokens with table structure classified as Table");
                Console.WriteLine($"  Reasoning: {result9.Reasoning}");
                Console.WriteLine($"  Rows: {result9.RowCount}, Columns: {result9.ColumnCount}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Table, got {result9.PageType}");
                Console.WriteLine($"  Reasoning: {result9.Reasoning}\n");
            }

            Console.WriteLine("\nAll tests completed!");
        }
    }
}
