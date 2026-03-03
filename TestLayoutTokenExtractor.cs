using System;
using System.Linq;
using OCRTool.Infrastructure.Layout;

namespace OCRTool
{
    /// <summary>
    /// Simple manual test for LayoutTokenExtractor TSV parsing
    /// Run these tests by calling TestLayoutTokenExtractor.RunAllTests() from the application
    /// </summary>
    public class TestLayoutTokenExtractor
    {
        public static void RunAllTests()
        {
            Console.WriteLine("Testing LayoutTokenExtractor.ExtractFromTesseractTsv");
            Console.WriteLine("=======================================================\n");

            var extractor = new LayoutTokenExtractor();

            // Sample TSV output from Tesseract (typical format)
            string sampleTsv = @"level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
1	1	0	0	0	0	0	0	1000	1000	-1	
2	1	1	0	0	0	100	100	800	50	-1	
3	1	1	1	0	0	100	100	800	50	-1	
4	1	1	1	1	0	100	100	800	50	-1	
5	1	1	1	1	1	100	100	150	40	95	TAG-001
5	1	1	1	1	2	260	100	200	40	87	EQUIPMENT
5	1	1	1	1	3	470	100	180	40	92	DESCRIPTION
2	1	2	0	0	0	100	200	800	50	-1	
3	1	2	1	0	0	100	200	800	50	-1	
4	1	2	1	1	0	100	200	800	50	-1	
5	1	2	1	1	1	100	200	150	40	98	TAG-002
5	1	2	1	1	2	260	200	200	40	85	MOTOR
5	1	2	1	1	3	470	200	180	40	90	PUMP";

            // Test 1: Parse valid TSV
            Console.WriteLine("Test 1: Parse valid TSV with word-level entries");
            var tokens = extractor.ExtractFromTesseractTsv(sampleTsv, 1);
            Console.WriteLine($"Extracted {tokens.Count} tokens");
            
            foreach (var token in tokens)
            {
                Console.WriteLine($"  Text: '{token.Text}', X: {token.X}, Y: {token.Y}, " +
                                $"Width: {token.Width}, Height: {token.Height}, " +
                                $"Confidence: {token.Confidence}, Page: {token.PageNumber}");
            }

            // Verify we only got level 5 (word-level) entries
            int expectedCount = 6; // 6 words in the sample
            if (tokens.Count == expectedCount)
            {
                Console.WriteLine($"✓ PASS: Got expected {expectedCount} word-level tokens\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected {expectedCount} tokens, got {tokens.Count}\n");
            }

            // Test 2: Empty input
            Console.WriteLine("Test 2: Empty input");
            var emptyTokens = extractor.ExtractFromTesseractTsv("", 1);
            if (emptyTokens.Count == 0)
            {
                Console.WriteLine("✓ PASS: Empty input returns empty collection\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 0 tokens, got {emptyTokens.Count}\n");
            }

            // Test 3: Null input
            Console.WriteLine("Test 3: Null input");
            var nullTokens = extractor.ExtractFromTesseractTsv(null!, 1);
            if (nullTokens.Count == 0)
            {
                Console.WriteLine("✓ PASS: Null input returns empty collection\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 0 tokens, got {nullTokens.Count}\n");
            }

            // Test 4: Malformed TSV (missing columns)
            Console.WriteLine("Test 4: Malformed TSV (missing columns)");
            string malformedTsv = @"level	page_num	block_num
5	1	1";
            var malformedTokens = extractor.ExtractFromTesseractTsv(malformedTsv, 1);
            if (malformedTokens.Count == 0)
            {
                Console.WriteLine("✓ PASS: Malformed TSV returns empty collection\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 0 tokens, got {malformedTokens.Count}\n");
            }

            // Test 5: Confidence score clamping
            Console.WriteLine("Test 5: Confidence score clamping (0-100 range)");
            string confidenceTsv = @"level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
5	1	1	1	1	1	100	100	150	40	150	OVER
5	1	1	1	1	2	260	100	200	40	-50	UNDER
5	1	1	1	1	3	470	100	180	40	50	NORMAL";
            var confTokens = extractor.ExtractFromTesseractTsv(confidenceTsv, 1);
            
            bool allClamped = confTokens.All(t => t.Confidence >= 0 && t.Confidence <= 100);
            if (allClamped && confTokens.Count == 3)
            {
                Console.WriteLine("✓ PASS: Confidence scores clamped to 0-100 range");
                foreach (var token in confTokens)
                {
                    Console.WriteLine($"  {token.Text}: Confidence = {token.Confidence}");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Confidence clamping failed\n");
            }

            // Test 6: Empty text entries are skipped
            Console.WriteLine("Test 6: Empty text entries are skipped");
            string emptyTextTsv = @"level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
5	1	1	1	1	1	100	100	150	40	95	TAG-001
5	1	1	1	1	2	260	100	200	40	87	
5	1	1	1	1	3	470	100	180	40	92	VALID";
            var emptyTextTokens = extractor.ExtractFromTesseractTsv(emptyTextTsv, 1);
            if (emptyTextTokens.Count == 2)
            {
                Console.WriteLine($"✓ PASS: Empty text entries skipped (got {emptyTextTokens.Count} tokens)\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected 2 tokens, got {emptyTextTokens.Count}\n");
            }

            Console.WriteLine("\nAll tests completed!");

            // Test 7: Coordinate normalization
            Console.WriteLine("\nTest 7: Coordinate normalization (Tesseract to PdfPig)");
            var tesseractToken = new OCRTool.Core.Models.LayoutToken
            {
                Text = "TEST",
                X = 100,
                Y = 50,  // Tesseract: Y=0 at top
                Width = 150,
                Height = 40,
                Confidence = 95,
                PageNumber = 1
            };
            
            double pageHeight = 1000;
            double pageWidth = 800;
            
            var normalizedToken = extractor.NormalizeCoordinates(tesseractToken, pageWidth, pageHeight);
            
            // Expected: Y_pdfpig = pageHeight - Y_tesseract - Height
            // Expected: Y_pdfpig = 1000 - 50 - 40 = 910
            double expectedY = 910;
            
            if (Math.Abs(normalizedToken.Y - expectedY) < 0.001 && normalizedToken.X == 100)
            {
                Console.WriteLine($"✓ PASS: Coordinate normalization correct");
                Console.WriteLine($"  Original (Tesseract): X={tesseractToken.X}, Y={tesseractToken.Y}");
                Console.WriteLine($"  Normalized (PdfPig): X={normalizedToken.X}, Y={normalizedToken.Y}");
                Console.WriteLine($"  Expected Y: {expectedY}, Got Y: {normalizedToken.Y}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Expected Y={expectedY}, got Y={normalizedToken.Y}\n");
            }

            // Test 8: Coordinate validation (NaN, Infinity)
            Console.WriteLine("Test 8: Coordinate validation (NaN, Infinity)");
            var invalidToken = new OCRTool.Core.Models.LayoutToken
            {
                Text = "INVALID",
                X = double.NaN,
                Y = 50,
                Width = 150,
                Height = 40,
                Confidence = 95,
                PageNumber = 1
            };
            
            var validatedToken = extractor.NormalizeCoordinates(invalidToken, pageWidth, pageHeight);
            
            // Should return token unchanged if coordinates are invalid
            if (double.IsNaN(validatedToken.X) && validatedToken.Y == 50)
            {
                Console.WriteLine("✓ PASS: Invalid coordinates detected and token returned unchanged\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: Invalid coordinate handling failed\n");
            }

            // Test 9: Multiple coordinate normalizations
            Console.WriteLine("Test 9: Multiple coordinate normalizations");
            var token1 = new OCRTool.Core.Models.LayoutToken { Text = "TOP", X = 0, Y = 0, Width = 100, Height = 20, Confidence = 95, PageNumber = 1 };
            var token2 = new OCRTool.Core.Models.LayoutToken { Text = "MIDDLE", X = 0, Y = 490, Width = 100, Height = 20, Confidence = 95, PageNumber = 1 };
            var token3 = new OCRTool.Core.Models.LayoutToken { Text = "BOTTOM", X = 0, Y = 980, Width = 100, Height = 20, Confidence = 95, PageNumber = 1 };
            
            var norm1 = extractor.NormalizeCoordinates(token1, pageWidth, pageHeight);
            var norm2 = extractor.NormalizeCoordinates(token2, pageWidth, pageHeight);
            var norm3 = extractor.NormalizeCoordinates(token3, pageWidth, pageHeight);
            
            // TOP: Y_pdfpig = 1000 - 0 - 20 = 980 (near top in PdfPig)
            // MIDDLE: Y_pdfpig = 1000 - 490 - 20 = 490 (middle)
            // BOTTOM: Y_pdfpig = 1000 - 980 - 20 = 0 (near bottom in PdfPig)
            
            bool test9Pass = Math.Abs(norm1.Y - 980) < 0.001 && 
                            Math.Abs(norm2.Y - 490) < 0.001 && 
                            Math.Abs(norm3.Y - 0) < 0.001;
            
            if (test9Pass)
            {
                Console.WriteLine("✓ PASS: Multiple normalizations correct");
                Console.WriteLine($"  TOP: Tesseract Y=0 → PdfPig Y={norm1.Y}");
                Console.WriteLine($"  MIDDLE: Tesseract Y=490 → PdfPig Y={norm2.Y}");
                Console.WriteLine($"  BOTTOM: Tesseract Y=980 → PdfPig Y={norm3.Y}\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL: Multiple normalizations failed\n");
            }

            Console.WriteLine("\nAll tests completed!");
        }
    }
}
