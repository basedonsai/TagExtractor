using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OCRTool.Infrastructure.PDF
{
    /// <summary>
    /// PDF processor using PdfPig library
    /// Detects if pages are searchable or scanned
    /// </summary>
    public class PdfPigPDFProcessor : IPDFProcessor
    {
        /// <summary>
        /// Extract pages from a PDF file
        /// </summary>
        public List<PageResult> ExtractPages(string pdfPath)
        {
            var results = new List<PageResult>();

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"PDF file not found: {pdfPath}");
            }

            try
            {
                using var document = PdfDocument.Open(pdfPath);

                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    var pageResult = AnalyzePage(page, i);
                    results.Add(pageResult);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to process PDF: {ex.Message}", ex);
            }

            return results;
        }

        /// <summary>
        /// Analyze a page to determine if it's searchable or scanned
        /// </summary>
        private PageResult AnalyzePage(UglyToad.PdfPig.Content.Page page, int pageNumber)
        {
            // Get text from the page
            var text = page.Text;
            var textLength = text?.Length ?? 0;

            // DEBUG: Add logging to see what text we're getting
            System.Diagnostics.Debug.WriteLine($"[PDF] Page {pageNumber}: Text length = {textLength}");
            if (!string.IsNullOrWhiteSpace(text) && textLength > 0)
            {
                var preview = text.Length > 100 ? text.Substring(0, 100) + "..." : text;
                System.Diagnostics.Debug.WriteLine($"[PDF] Page {pageNumber}: Text preview = {preview.Replace("\n", " ").Replace("\r", "")}");
            }

            // Check for embedded text
            var hasText = !string.IsNullOrWhiteSpace(text) && textLength > 10;

            // Determine if searchable or scanned
            // A page is searchable if it has significant text content
            var isSearchable = hasText && textLength > 50;

            // Extract images for OCR - Python approach: always extract images
            byte[]? imageData = null;
            
            // Python's approach: Always extract page as image for OCR
            // This ensures we get the same OCR results as Python
            imageData = ExtractPageImage(page);
            
            // If image extraction failed, try alternative method
            if (imageData == null)
            {
                imageData = TryRenderPageToImage(page);
            }

            return new PageResult
            {
                PageNumber = pageNumber,
                IsSearchable = isSearchable,
                RawText = text ?? string.Empty,
                ImageData = imageData,
                TextLength = textLength
            };
        }

        /// <summary>
        /// Extract images from a page using reflection to access internal methods
        /// </summary>
        private byte[]? ExtractPageImage(UglyToad.PdfPig.Content.Page page)
        {
            try
            {
                var images = page.GetImages().ToList();

                if (images.Any())
                {
                    // Try to get image bytes from the first image using reflection
                    foreach (var img in images)
                    {
                        try
                        {
                            var bytes = GetImageBytesViaReflection(img);
                            if (bytes != null && bytes.Length > 1000) // Minimum size check
                            {
                                return bytes;
                            }
                        }
                        catch
                        {
                            // Continue to next image
                        }
                    }
                }
            }
            catch
            {
                // If image extraction fails, return null
            }

            return null;
        }

        /// <summary>
        /// Try to get image bytes using reflection
        /// </summary>
        private byte[]? GetImageBytesViaReflection(object image)
        {
            try
            {
                // Try to find Bytes property or field
                var type = image.GetType();
                
                // Try property first
                var bytesProperty = type.GetProperty("Bytes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bytesProperty != null)
                {
                    var value = bytesProperty.GetValue(image) as byte[];
                    if (value != null && value.Length > 0)
                        return value;
                }
                
                // Try field
                var bytesField = type.GetField("Bytes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bytesField != null)
                {
                    var value = bytesField.GetValue(image) as byte[];
                    if (value != null && value.Length > 0)
                        return value;
                }
                
                // Try GetInternalImageBytes method
                var method = type.GetMethod("GetInternalImageBytes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var result = method.Invoke(image, null);
                    if (result is byte[] b && b.Length > 0)
                        return b;
                }
                
                // Try ImageBytes property
                var imageBytesProp = type.GetProperty("ImageBytes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (imageBytesProp != null)
                {
                    var value = imageBytesProp.GetValue(image) as byte[];
                    if (value != null && value.Length > 0)
                        return value;
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Try to render page to image using System.Drawing
        /// </summary>
        private byte[]? TryRenderPageToImage(UglyToad.PdfPig.Content.Page page)
        {
            try
            {
                // Create a bitmap for the page
                var width = (int)(page.Width * 2); // Scale for better OCR
                var height = (int)(page.Height * 2);
                
                using var bitmap = new System.Drawing.Bitmap(width, height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                
                // Set white background
                graphics.Clear(System.Drawing.Color.White);
                
                // Draw text content
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                
                var font = new System.Drawing.Font("Arial", 12);
                var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                
                // Extract and draw text words
                foreach (var word in page.GetWords())
                {
                    var x = (float)(word.BoundingBox.Left * 2);
                    var y = (float)(height - word.BoundingBox.Top * 2); // Flip Y coordinate
                    
                    graphics.DrawString(word.Text, font, brush, x, y);
                }
                
                font.Dispose();
                brush.Dispose();
                
                // Convert to byte array
                using var stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PDF] Failed to render page to image: {ex.Message}");
                return null;
            }
        }
    }
}
