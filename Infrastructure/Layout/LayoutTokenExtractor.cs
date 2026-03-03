using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using UglyToad.PdfPig.Content;

namespace OCRTool.Infrastructure.Layout
{
    /// <summary>
    /// Extracts layout tokens with spatial metadata from PdfPig pages and Tesseract OCR output
    /// </summary>
    public class LayoutTokenExtractor : ILayoutTokenExtractor
    {
        /// <summary>
        /// Extract layout tokens from PdfPig page
        /// </summary>
        /// <param name="page">PdfPig page object containing text elements</param>
        /// <param name="pageNumber">Page number for token metadata</param>
        /// <returns>List of layout tokens with bounding boxes and spatial metadata</returns>
        public List<LayoutToken> ExtractFromPdfPigPage(Page page, int pageNumber)
        {
            var tokens = new List<LayoutToken>();

            if (page == null)
            {
                return tokens; // Return empty collection, not null
            }

            try
            {
                // Extract words from PdfPig page
                var words = page.GetWords();

                foreach (var word in words)
                {
                    try
                    {
                        // Extract bounding box coordinates from PdfPig word element
                        var bbox = word.BoundingBox;

                        // Create LayoutToken with confidence = 100 for searchable PDF text
                        var token = new LayoutToken
                        {
                            Text = word.Text ?? string.Empty,
                            X = bbox.Left,
                            Y = bbox.Bottom, // PdfPig uses bottom-left origin
                            Width = bbox.Width,
                            Height = bbox.Height,
                            Confidence = 100, // Searchable PDF text has 100% confidence
                            PageNumber = pageNumber
                        };

                        tokens.Add(token);
                    }
                    catch (Exception ex)
                    {
                        // Log error for individual word but continue processing
                        System.Diagnostics.Debug.WriteLine($"[LayoutTokenExtractor] Error extracting word on page {pageNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and return empty collection on extraction failure
                System.Diagnostics.Debug.WriteLine($"[LayoutTokenExtractor] Error extracting layout tokens from PdfPig page {pageNumber}: {ex.Message}");
                return new List<LayoutToken>(); // Return empty collection, not null
            }

            return tokens;
        }

        /// <summary>
        /// Extract layout tokens from Tesseract TSV output
        /// </summary>
        /// <param name="tsvOutput">TSV format output from Tesseract OCR</param>
        /// <param name="pageNumber">Page number for token metadata</param>
        /// <returns>List of layout tokens with bounding boxes and spatial metadata</returns>
        public List<LayoutToken> ExtractFromTesseractTsv(string tsvOutput, int pageNumber)
        {
            var tokens = new List<LayoutToken>();

            if (string.IsNullOrWhiteSpace(tsvOutput))
            {
                return tokens; // Return empty collection, not null
            }

            try
            {
                // Split TSV output into lines
                var lines = tsvOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // Skip header line (first line contains column names)
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var line = lines[i];
                        var columns = line.Split('\t');

                        // TSV format: level, page_num, block_num, par_num, line_num, word_num, left, top, width, height, conf, text
                        // We need at least 12 columns
                        if (columns.Length < 12)
                        {
                            continue; // Skip malformed lines
                        }

                        // Extract level (column 0) - we only want word-level entries (level 5)
                        if (!int.TryParse(columns[0], out int level) || level != 5)
                        {
                            continue; // Skip non-word-level entries
                        }

                        // Extract bounding box coordinates
                        if (!double.TryParse(columns[6], out double left))
                        {
                            continue; // Skip if left coordinate is invalid
                        }

                        if (!double.TryParse(columns[7], out double top))
                        {
                            continue; // Skip if top coordinate is invalid
                        }

                        if (!double.TryParse(columns[8], out double width))
                        {
                            continue; // Skip if width is invalid
                        }

                        if (!double.TryParse(columns[9], out double height))
                        {
                            continue; // Skip if height is invalid
                        }

                        // Extract confidence score (0-100 range)
                        if (!double.TryParse(columns[10], out double confidence))
                        {
                            confidence = 0; // Default to 0 if confidence is invalid
                        }

                        // Clamp confidence to 0-100 range
                        confidence = Math.Max(0, Math.Min(100, confidence));

                        // Extract text content (column 11, may contain tabs or be empty)
                        var text = columns.Length > 11 ? columns[11] : string.Empty;

                        // Skip empty text entries
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            continue;
                        }

                        // Create LayoutToken with Tesseract coordinates (top-left origin)
                        var token = new LayoutToken
                        {
                            Text = text.Trim(),
                            X = left,
                            Y = top,
                            Width = width,
                            Height = height,
                            Confidence = confidence,
                            PageNumber = pageNumber
                        };

                        tokens.Add(token);
                    }
                    catch (Exception ex)
                    {
                        // Log error for individual line but continue processing
                        System.Diagnostics.Debug.WriteLine($"[LayoutTokenExtractor] Error parsing TSV line {i} on page {pageNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and return empty collection on parsing failure
                System.Diagnostics.Debug.WriteLine($"[LayoutTokenExtractor] Error extracting layout tokens from Tesseract TSV on page {pageNumber}: {ex.Message}");
                return new List<LayoutToken>(); // Return empty collection, not null
            }

            return tokens;
        }

        /// <summary>
        /// Normalize Tesseract coordinates to PdfPig coordinate system
        /// </summary>
        /// <param name="token">Layout token with Tesseract coordinates</param>
        /// <param name="pageWidth">Width of the page</param>
        /// <param name="pageHeight">Height of the page</param>
        /// <returns>Layout token with normalized coordinates in PdfPig coordinate system</returns>
        public LayoutToken NormalizeCoordinates(LayoutToken token, double pageWidth, double pageHeight)
        {
            // Validate input coordinates
            if (double.IsNaN(token.X) || double.IsInfinity(token.X) ||
                double.IsNaN(token.Y) || double.IsInfinity(token.Y) ||
                double.IsNaN(token.Width) || double.IsInfinity(token.Width) ||
                double.IsNaN(token.Height) || double.IsInfinity(token.Height) ||
                double.IsNaN(pageWidth) || double.IsInfinity(pageWidth) ||
                double.IsNaN(pageHeight) || double.IsInfinity(pageHeight))
            {
                // Return token unchanged if coordinates are invalid
                System.Diagnostics.Debug.WriteLine($"[LayoutTokenExtractor] Invalid coordinates detected for token '{token.Text}' on page {token.PageNumber}");
                return token;
            }

            // Convert Tesseract top-left origin to PdfPig bottom-left origin
            // Tesseract: Y=0 is at top, increases downward
            // PdfPig: Y=0 is at bottom, increases upward
            // Formula: Y_pdfpig = pageHeight - Y_tesseract - Height
            var normalizedY = pageHeight - token.Y - token.Height;

            // Create new token with normalized coordinates
            var normalizedToken = new LayoutToken
            {
                Text = token.Text,
                X = token.X, // X coordinate remains the same (both use left edge)
                Y = normalizedY,
                Width = token.Width,
                Height = token.Height,
                Confidence = token.Confidence,
                PageNumber = token.PageNumber
            };

            return normalizedToken;
        }
    }
}
