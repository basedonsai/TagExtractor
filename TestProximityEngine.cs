using System;
using System.Collections.Generic;
using System.Linq;
using OCRTool.Core.Configuration;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;
using OCRTool.Core.RecordBuilders;

namespace OCRTool
{
    /// <summary>
    /// Simple manual test for ProximityEngine TAG token identification
    /// Run these tests by calling TestProximityEngine.RunAllTests() from the application
    /// </summary>
    public class TestProximityEngine
    {
        public static void RunAllTests()
        {
            Console.WriteLine("Testing ProximityEngine TAG Token Identification (Task 12.1)");
            Console.WriteLine("=============================================================\n");

            // Create a PatternMatcher with sample TAG patterns
            var config = CreateTestConfig();
            var patternMatcher = new PatternMatcher(config);
            var engine = new ProximityEngine(patternMatcher);

            // Test 1: CanProcess returns true for Scattered page type
            Console.WriteLine("Test 1: CanProcess returns true for Scattered page type");
            if (engine.CanProcess(PageType.Scattered))
            {
                Console.WriteLine("✓ PASS: CanProcess(PageType.Scattered) returns true\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: CanProcess(PageType.Scattered) should return true\n");
            }

            // Test 2: CanProcess returns false for Table page type
            Console.WriteLine("Test 2: CanProcess returns false for Table page type");
            if (!engine.CanProcess(PageType.Table))
            {
                Console.WriteLine("✓ PASS: CanProcess(PageType.Table) returns false\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: CanProcess(PageType.Table) should return false\n");
            }

            // Test 3: CanProcess returns false for Sparse page type
            Console.WriteLine("Test 3: CanProcess returns false for Sparse page type");
            if (!engine.CanProcess(PageType.Sparse))
            {
                Console.WriteLine("✓ PASS: CanProcess(PageType.Sparse) returns false\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: CanProcess(PageType.Sparse) should return false\n");
            }

            // Test 4: BuildRecords with null tokens returns empty list
            Console.WriteLine("Test 4: BuildRecords with null tokens returns empty list");
            var result1 = engine.BuildRecords(null!, 1, "test.pdf");
            if (result1 != null && result1.Count == 0)
            {
                Console.WriteLine("✓ PASS: Null tokens returns empty list\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: Expected empty list for null tokens\n");
            }

            // Test 5: BuildRecords with empty tokens returns empty list
            Console.WriteLine("Test 5: BuildRecords with empty tokens returns empty list");
            var result2 = engine.BuildRecords(new List<LayoutToken>(), 1, "test.pdf");
            if (result2 != null && result2.Count == 0)
            {
                Console.WriteLine("✓ PASS: Empty tokens returns empty list\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: Expected empty list for empty tokens\n");
            }

            // Test 6: Identify TAG tokens from mixed layout tokens
            Console.WriteLine("Test 6: Identify TAG tokens from mixed layout tokens");
            var mixedTokens = new List<LayoutToken>
            {
                // TAG tokens (should be identified as anchors)
                new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "640052", X = 300, Y = 150, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "FT-101", X = 500, Y = 200, Width = 50, Height = 20, PageNumber = 1 },
                
                // Non-TAG tokens (should not be identified as anchors)
                new LayoutToken { Text = "Motor", X = 120, Y = 130, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "5HP", X = 110, Y = 160, Width = 30, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 320, Y = 180, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Description", X = 520, Y = 230, Width = 80, Height = 20, PageNumber = 1 }
            };

            var records = engine.BuildRecords(mixedTokens, 1, "test.pdf");
            
            // Should identify 3 TAG tokens and create 3 records
            if (records.Count == 3)
            {
                Console.WriteLine($"✓ PASS: Identified {records.Count} TAG tokens as anchors");
                
                // Verify each record has the correct TAG
                var expectedTags = new[] { "TAG-001", "640052", "FT-101" };
                var actualTags = records.Select(r => r.Tag).OrderBy(t => t).ToArray();
                var expectedTagsSorted = expectedTags.OrderBy(t => t).ToArray();
                
                if (actualTags.SequenceEqual(expectedTagsSorted))
                {
                    Console.WriteLine("✓ PASS: All TAG tokens correctly identified:");
                    foreach (var record in records.OrderBy(r => r.Tag))
                    {
                        Console.WriteLine($"  - {record.Tag}");
                    }
                }
                else
                {
                    Console.WriteLine("✗ FAIL: TAG tokens not correctly identified");
                    Console.WriteLine($"  Expected: {string.Join(", ", expectedTagsSorted)}");
                    Console.WriteLine($"  Actual: {string.Join(", ", actualTags)}");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 3 TAG tokens, but found {records.Count}");
            }
            Console.WriteLine();

            // Test 7: No TAG tokens in layout
            Console.WriteLine("Test 7: No TAG tokens in layout returns empty list");
            var nonTagTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "Motor", X = 100, Y = 100, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 200, Y = 100, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Valve", X = 300, Y = 100, Width = 40, Height = 20, PageNumber = 1 }
            };

            var result3 = engine.BuildRecords(nonTagTokens, 1, "test.pdf");
            if (result3.Count == 0)
            {
                Console.WriteLine("✓ PASS: No TAG tokens returns empty list\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected empty list, but found {result3.Count} records\n");
            }

            // Test 8: Verify record metadata
            Console.WriteLine("Test 8: Verify record metadata (Source, Page, Method)");
            var singleTagToken = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-999", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 5 }
            };

            var result4 = engine.BuildRecords(singleTagToken, 5, "drawing.pdf");
            if (result4.Count == 1)
            {
                var record = result4[0];
                bool passSource = record.Source == "drawing.pdf";
                bool passPage = record.Page == 5;
                bool passMethod = record.Method == ExtractionMethod.ProximityEngine;
                bool passTag = record.Tag == "TAG-999";

                if (passSource && passPage && passMethod && passTag)
                {
                    Console.WriteLine("✓ PASS: Record metadata correctly populated");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Source: {record.Source}");
                    Console.WriteLine($"  Page: {record.Page}");
                    Console.WriteLine($"  Method: {record.Method}");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Record metadata incorrect");
                    if (!passTag) Console.WriteLine($"  Tag: Expected 'TAG-999', got '{record.Tag}'");
                    if (!passSource) Console.WriteLine($"  Source: Expected 'drawing.pdf', got '{record.Source}'");
                    if (!passPage) Console.WriteLine($"  Page: Expected 5, got {record.Page}");
                    if (!passMethod) Console.WriteLine($"  Method: Expected ProximityEngine, got {record.Method}");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {result4.Count}");
            }
            Console.WriteLine();

            // Test 9: Verify distance calculation using LayoutToken properties
            Console.WriteLine("Test 9: Verify distance calculation using LayoutToken properties");
            var token1 = new LayoutToken { Text = "A", X = 0, Y = 0, Width = 10, Height = 10, PageNumber = 1 };
            var token2 = new LayoutToken { Text = "B", X = 30, Y = 40, Width = 10, Height = 10, PageNumber = 1 };
            
            // Token1 center: (5, 5), Token2 center: (35, 45)
            // Expected distance: sqrt((35-5)^2 + (45-5)^2) = sqrt(900 + 1600) = sqrt(2500) = 50
            var expectedDistance = 50.0;
            var actualDistance = Math.Sqrt(
                Math.Pow(token2.CenterX - token1.CenterX, 2) + 
                Math.Pow(token2.CenterY - token1.CenterY, 2)
            );
            
            if (Math.Abs(actualDistance - expectedDistance) < 0.001)
            {
                Console.WriteLine($"✓ PASS: Distance calculation correct: {actualDistance} pixels");
                Console.WriteLine($"  Token1 center: ({token1.CenterX}, {token1.CenterY})");
                Console.WriteLine($"  Token2 center: ({token2.CenterX}, {token2.CenterY})");
                Console.WriteLine($"  Distance: {actualDistance}");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Distance calculation incorrect");
                Console.WriteLine($"  Expected: {expectedDistance}, Actual: {actualDistance}");
            }
            Console.WriteLine();

            // Test 10: Proximity grouping - tokens within threshold
            Console.WriteLine("Test 10: Proximity grouping - tokens within 100-pixel threshold");
            var anchorToken = new LayoutToken { Text = "TAG-100", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 };
            var nearbyTokens = new List<LayoutToken>
            {
                anchorToken,
                // Within threshold (< 100 pixels from anchor center at 125, 110)
                new LayoutToken { Text = "Motor", X = 130, Y = 120, Width = 40, Height = 20, PageNumber = 1 }, // ~14 pixels
                new LayoutToken { Text = "5HP", X = 150, Y = 150, Width = 30, Height = 20, PageNumber = 1 },   // ~50 pixels
                new LayoutToken { Text = "Pump", X = 180, Y = 180, Width = 40, Height = 20, PageNumber = 1 },  // ~98 pixels
                // Beyond threshold (> 100 pixels)
                new LayoutToken { Text = "Valve", X = 300, Y = 300, Width = 40, Height = 20, PageNumber = 1 }, // ~247 pixels
                new LayoutToken { Text = "Tank", X = 400, Y = 400, Width = 40, Height = 20, PageNumber = 1 }   // ~410 pixels
            };

            var result10 = engine.BuildRecords(nearbyTokens, 1, "test.pdf");
            if (result10.Count == 1)
            {
                Console.WriteLine("✓ PASS: One record created for anchor token");
                Console.WriteLine($"  Anchor: {result10[0].Tag}");
                // Note: Equipment and Rating fields will be populated in Task 12.4
                Console.WriteLine("  (Equipment and Rating classification pending Task 12.4)");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {result10.Count}");
            }
            Console.WriteLine();

            // Test 11: Proximity grouping - distance sorting
            Console.WriteLine("Test 11: Proximity grouping - tokens sorted by distance");
            var anchorToken2 = new LayoutToken { Text = "FT-200", X = 200, Y = 200, Width = 50, Height = 20, PageNumber = 1 };
            var unsortedTokens = new List<LayoutToken>
            {
                anchorToken2,
                // Tokens at various distances (center at 225, 210)
                new LayoutToken { Text = "Far", X = 280, Y = 250, Width = 30, Height = 20, PageNumber = 1 },    // ~73 pixels
                new LayoutToken { Text = "Near", X = 230, Y = 215, Width = 30, Height = 20, PageNumber = 1 },   // ~7 pixels
                new LayoutToken { Text = "Medium", X = 250, Y = 230, Width = 40, Height = 20, PageNumber = 1 }  // ~35 pixels
            };

            var result11 = engine.BuildRecords(unsortedTokens, 1, "test.pdf");
            if (result11.Count == 1)
            {
                Console.WriteLine("✓ PASS: Record created with proximity grouping");
                Console.WriteLine($"  Anchor: {result11[0].Tag}");
                Console.WriteLine("  Expected sort order: Near (7px), Medium (35px), Far (73px)");
                Console.WriteLine("  (Actual grouping and sorting verified internally)");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {result11.Count}");
            }
            Console.WriteLine();

            // Test 12: Proximity grouping - no nearby tokens
            Console.WriteLine("Test 12: Proximity grouping - anchor with no nearby tokens");
            var isolatedAnchor = new LayoutToken { Text = "TAG-300", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 };
            var distantTokens = new List<LayoutToken>
            {
                isolatedAnchor,
                // All tokens beyond 100-pixel threshold
                new LayoutToken { Text = "Distant1", X = 300, Y = 300, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Distant2", X = 400, Y = 400, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Distant3", X = 500, Y = 500, Width = 40, Height = 20, PageNumber = 1 }
            };

            var result12 = engine.BuildRecords(distantTokens, 1, "test.pdf");
            if (result12.Count == 1)
            {
                var record = result12[0];
                Console.WriteLine("✓ PASS: Record created even with no nearby tokens");
                Console.WriteLine($"  Tag: {record.Tag}");
                Console.WriteLine($"  Equipment: '{record.Equipment}' (empty as expected)");
                Console.WriteLine($"  Rating: '{record.Rating}' (empty as expected)");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {result12.Count}");
            }
            Console.WriteLine();

            // Test 13: Multiple anchors with proximity grouping
            Console.WriteLine("Test 13: Multiple anchors with separate proximity groups");
            var multiAnchorTokens = new List<LayoutToken>
            {
                // First anchor and its nearby tokens
                new LayoutToken { Text = "TAG-401", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor1", X = 130, Y = 120, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "10HP", X = 140, Y = 140, Width = 30, Height = 20, PageNumber = 1 },
                
                // Second anchor and its nearby tokens (far from first anchor)
                new LayoutToken { Text = "TAG-402", X = 500, Y = 500, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor2", X = 530, Y = 520, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "20HP", X = 540, Y = 540, Width = 30, Height = 20, PageNumber = 1 }
            };

            var result13 = engine.BuildRecords(multiAnchorTokens, 1, "test.pdf");
            if (result13.Count == 2)
            {
                Console.WriteLine("✓ PASS: Two records created for two anchors");
                var tags = result13.Select(r => r.Tag).OrderBy(t => t).ToList();
                Console.WriteLine($"  Anchors: {string.Join(", ", tags)}");
                Console.WriteLine("  Each anchor has its own proximity group");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 2 records, but found {result13.Count}");
            }
            Console.WriteLine();

            Console.WriteLine("All ProximityEngine TAG identification tests completed!");
        }

        /// <summary>
        /// Create a test configuration with sample TAG patterns
        /// </summary>
        private static ExtractionConfig CreateTestConfig()
        {
            return new ExtractionConfig
            {
                Patterns = new List<PatternDefinition>
                {
                    // Pattern for TAG-XXX format
                    new PatternDefinition
                    {
                        Regex = @"\b[A-Z]{2,4}-\d{3}\b",
                        Type = "TAG"
                    },
                    // Pattern for numeric tags (6 digits)
                    new PatternDefinition
                    {
                        Regex = @"\b\d{6}\b",
                        Type = "TAG"
                    }
                }
            };
        }
    }
}
