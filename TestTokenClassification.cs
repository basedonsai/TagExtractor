using System;
using System.Collections.Generic;
using OCRTool.Core.Configuration;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;
using OCRTool.Core.RecordBuilders;

namespace OCRTool
{
    /// <summary>
    /// Simple manual test for ProximityEngine token classification (Task 12.4)
    /// Run these tests by calling TestTokenClassification.RunAllTests() from the application
    /// </summary>
    public class TestTokenClassification
    {
        public static void RunAllTests()
        {
            Console.WriteLine("Testing ProximityEngine Token Classification (Task 12.4)");
            Console.WriteLine("=========================================================\n");

            // Create a PatternMatcher with sample TAG patterns
            var config = CreateTestConfig();
            var patternMatcher = new PatternMatcher(config);
            var engine = new ProximityEngine(patternMatcher);

            // Test 1: Classify Equipment tokens (descriptive text)
            Console.WriteLine("Test 1: Classify Equipment tokens (descriptive text)");
            var equipmentTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 120, Y = 130, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 130, Y = 140, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Valve", X = 140, Y = 150, Width = 40, Height = 20, PageNumber = 1 }
            };

            var records1 = engine.BuildRecords(equipmentTokens, 1, "test.pdf");
            if (records1.Count == 1)
            {
                var record = records1[0];
                var expectedEquipment = "Motor Pump Valve";
                if (record.Equipment == expectedEquipment && string.IsNullOrEmpty(record.Rating))
                {
                    Console.WriteLine("✓ PASS: Equipment tokens correctly classified");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Equipment: {record.Equipment}");
                    Console.WriteLine($"  Rating: '{record.Rating}' (empty as expected)");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Equipment classification incorrect");
                    Console.WriteLine($"  Expected Equipment: '{expectedEquipment}'");
                    Console.WriteLine($"  Actual Equipment: '{record.Equipment}'");
                    Console.WriteLine($"  Expected Rating: ''");
                    Console.WriteLine($"  Actual Rating: '{record.Rating}'");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {records1.Count}");
            }
            Console.WriteLine();

            // Test 2: Classify Rating tokens (numeric specifications)
            Console.WriteLine("Test 2: Classify Rating tokens (numeric specifications)");
            var ratingTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-002", X = 200, Y = 200, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "5HP", X = 220, Y = 230, Width = 30, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "240V", X = 230, Y = 240, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "60Hz", X = 240, Y = 250, Width = 40, Height = 20, PageNumber = 1 }
            };

            var records2 = engine.BuildRecords(ratingTokens, 1, "test.pdf");
            if (records2.Count == 1)
            {
                var record = records2[0];
                var expectedRating = "5HP 240V 60Hz";
                if (record.Rating == expectedRating && string.IsNullOrEmpty(record.Equipment))
                {
                    Console.WriteLine("✓ PASS: Rating tokens correctly classified");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Equipment: '{record.Equipment}' (empty as expected)");
                    Console.WriteLine($"  Rating: {record.Rating}");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Rating classification incorrect");
                    Console.WriteLine($"  Expected Equipment: ''");
                    Console.WriteLine($"  Actual Equipment: '{record.Equipment}'");
                    Console.WriteLine($"  Expected Rating: '{expectedRating}'");
                    Console.WriteLine($"  Actual Rating: '{record.Rating}'");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {records2.Count}");
            }
            Console.WriteLine();

            // Test 3: Mixed Equipment and Rating tokens
            Console.WriteLine("Test 3: Mixed Equipment and Rating tokens");
            var mixedTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-003", X = 300, Y = 300, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 320, Y = 330, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "10HP", X = 330, Y = 340, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 340, Y = 350, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "3.5A", X = 350, Y = 360, Width = 40, Height = 20, PageNumber = 1 }
            };

            var records3 = engine.BuildRecords(mixedTokens, 1, "test.pdf");
            if (records3.Count == 1)
            {
                var record = records3[0];
                var expectedEquipment = "Motor Pump";
                var expectedRating = "10HP 3.5A";
                if (record.Equipment == expectedEquipment && record.Rating == expectedRating)
                {
                    Console.WriteLine("✓ PASS: Mixed tokens correctly classified");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Equipment: {record.Equipment}");
                    Console.WriteLine($"  Rating: {record.Rating}");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Mixed token classification incorrect");
                    Console.WriteLine($"  Expected Equipment: '{expectedEquipment}'");
                    Console.WriteLine($"  Actual Equipment: '{record.Equipment}'");
                    Console.WriteLine($"  Expected Rating: '{expectedRating}'");
                    Console.WriteLine($"  Actual Rating: '{record.Rating}'");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {records3.Count}");
            }
            Console.WriteLine();

            // Test 4: Pure numeric ratings
            Console.WriteLine("Test 4: Pure numeric ratings (no units)");
            var numericTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-004", X = 400, Y = 400, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "100", X = 420, Y = 430, Width = 30, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "200", X = 430, Y = 440, Width = 30, Height = 20, PageNumber = 1 }
            };

            var records4 = engine.BuildRecords(numericTokens, 1, "test.pdf");
            if (records4.Count == 1)
            {
                var record = records4[0];
                var expectedRating = "100 200";
                if (record.Rating == expectedRating && string.IsNullOrEmpty(record.Equipment))
                {
                    Console.WriteLine("✓ PASS: Pure numeric tokens classified as Rating");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Equipment: '{record.Equipment}' (empty as expected)");
                    Console.WriteLine($"  Rating: {record.Rating}");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Pure numeric classification incorrect");
                    Console.WriteLine($"  Expected Equipment: ''");
                    Console.WriteLine($"  Actual Equipment: '{record.Equipment}'");
                    Console.WriteLine($"  Expected Rating: '{expectedRating}'");
                    Console.WriteLine($"  Actual Rating: '{record.Rating}'");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {records4.Count}");
            }
            Console.WriteLine();

            // Test 5: Distance sorting with classification
            Console.WriteLine("Test 5: Distance sorting with classification");
            var sortedTokens = new List<LayoutToken>
            {
                new LayoutToken { Text = "TAG-005", X = 500, Y = 500, Width = 50, Height = 20, PageNumber = 1 },
                // Tokens at various distances (sorted by distance after grouping)
                new LayoutToken { Text = "Far", X = 580, Y = 550, Width = 30, Height = 20, PageNumber = 1 },    // ~90 pixels
                new LayoutToken { Text = "Near", X = 510, Y = 515, Width = 30, Height = 20, PageNumber = 1 },   // ~16 pixels
                new LayoutToken { Text = "10HP", X = 550, Y = 530, Width = 40, Height = 20, PageNumber = 1 },   // ~58 pixels
                new LayoutToken { Text = "Medium", X = 530, Y = 520, Width = 40, Height = 20, PageNumber = 1 }  // ~36 pixels
            };

            var records5 = engine.BuildRecords(sortedTokens, 1, "test.pdf");
            if (records5.Count == 1)
            {
                var record = records5[0];
                // Expected order by distance: Near (16px), Medium (36px), 10HP (58px), Far (90px)
                // Equipment: Near, Medium, Far (alphabetic tokens)
                // Rating: 10HP (numeric token)
                var expectedEquipment = "Near Medium Far";
                var expectedRating = "10HP";
                if (record.Equipment == expectedEquipment && record.Rating == expectedRating)
                {
                    Console.WriteLine("✓ PASS: Tokens sorted by distance and classified correctly");
                    Console.WriteLine($"  Tag: {record.Tag}");
                    Console.WriteLine($"  Equipment: {record.Equipment} (sorted by distance)");
                    Console.WriteLine($"  Rating: {record.Rating}");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Distance sorting or classification incorrect");
                    Console.WriteLine($"  Expected Equipment: '{expectedEquipment}'");
                    Console.WriteLine($"  Actual Equipment: '{record.Equipment}'");
                    Console.WriteLine($"  Expected Rating: '{expectedRating}'");
                    Console.WriteLine($"  Actual Rating: '{record.Rating}'");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 1 record, but found {records5.Count}");
            }
            Console.WriteLine();

            // Test 6: Multiple anchors with separate classifications
            Console.WriteLine("Test 6: Multiple anchors with separate classifications");
            var multiAnchorTokens = new List<LayoutToken>
            {
                // First anchor with equipment
                new LayoutToken { Text = "TAG-006", X = 100, Y = 100, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Motor", X = 120, Y = 130, Width = 40, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "Pump", X = 130, Y = 140, Width = 40, Height = 20, PageNumber = 1 },
                
                // Second anchor with ratings (far from first anchor)
                new LayoutToken { Text = "TAG-007", X = 500, Y = 500, Width = 50, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "5HP", X = 520, Y = 530, Width = 30, Height = 20, PageNumber = 1 },
                new LayoutToken { Text = "240V", X = 530, Y = 540, Width = 40, Height = 20, PageNumber = 1 }
            };

            var records6 = engine.BuildRecords(multiAnchorTokens, 1, "test.pdf");
            if (records6.Count == 2)
            {
                var record1 = records6.Find(r => r.Tag == "TAG-006");
                var record2 = records6.Find(r => r.Tag == "TAG-007");
                
                if (record1 != null && record2 != null)
                {
                    bool pass1 = record1.Equipment == "Motor Pump" && string.IsNullOrEmpty(record1.Rating);
                    bool pass2 = record2.Rating == "5HP 240V" && string.IsNullOrEmpty(record2.Equipment);
                    
                    if (pass1 && pass2)
                    {
                        Console.WriteLine("✓ PASS: Multiple anchors with separate classifications");
                        Console.WriteLine($"  {record1.Tag}: Equipment='{record1.Equipment}', Rating='{record1.Rating}'");
                        Console.WriteLine($"  {record2.Tag}: Equipment='{record2.Equipment}', Rating='{record2.Rating}'");
                    }
                    else
                    {
                        Console.WriteLine("✗ FAIL: Multiple anchor classification incorrect");
                        Console.WriteLine($"  {record1.Tag}: Equipment='{record1.Equipment}', Rating='{record1.Rating}'");
                        Console.WriteLine($"  {record2.Tag}: Equipment='{record2.Equipment}', Rating='{record2.Rating}'");
                    }
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Could not find expected records");
                }
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 2 records, but found {records6.Count}");
            }
            Console.WriteLine();

            Console.WriteLine("All ProximityEngine token classification tests completed!");
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
