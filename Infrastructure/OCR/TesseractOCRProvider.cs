using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using System;
using System.IO;
using Tesseract;

namespace OCRTool.Infrastructure.OCR
{
    /// <summary>
    /// Tesseract OCR implementation using Tesseract.NET
    /// This wraps the Tesseract engine and hides it from the business layer
    /// </summary>
    public class TesseractOCRProvider : IOCRProvider, IDisposable
    {
        private readonly TesseractEngine _engine;
        private readonly string _tessdataPath;
        private bool _disposed;

        public TesseractOCRProvider(string tessdataPath)
        {
            _tessdataPath = tessdataPath;
            // Create engine with Python's exact configuration
            _engine = new TesseractEngine(tessdataPath, "eng", EngineMode.LstmOnly);
            // Python's exact config: --oem 3 --psm 6
            _engine.SetVariable("tessedit_ocr_engine_mode", "3");
            _engine.SetVariable("tessedit_pageseg_mode", "6");
            // Python's exact character whitelist
            _engine.SetVariable("tessedit_char_whitelist", "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+-./: ");
        }

        /// <summary>
        /// Process an image and extract text
        /// </summary>
        public ExtractionResult ProcessImage(byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                {
                    return CreateEmptyResult("No image data provided");
                }

                // Try to load from memory first
                try
                {
                    using var pix = Pix.LoadFromMemory(imageData);
                    return ProcessPix(pix);
                }
                catch (Exception ex)
                {
                    // If loading from memory fails, try saving to temp file and loading from file
                    // This handles cases where the image format is not directly supported
                    return TryLoadFromTempFile(imageData);
                }
            }
            catch (Exception ex)
            {
                return CreateEmptyResult($"OCR failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to save image data to temp file and load it
        /// </summary>
        private ExtractionResult TryLoadFromTempFile(byte[] imageData)
        {
            try
            {
                // Create a temp file
                var tempFile = Path.GetTempFileName();
                var tempPng = Path.ChangeExtension(tempFile, ".png");
                
                // Try to write the image data
                File.WriteAllBytes(tempPng, imageData);
                
                try
                {
                    using var pix = Pix.LoadFromFile(tempPng);
                    return ProcessPix(pix);
                }
                finally
                {
                    // Clean up temp file
                    try { File.Delete(tempPng); } catch { }
                    try { File.Delete(tempFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                return CreateEmptyResult($"OCR temp file failed: {ex.Message}");
            }
        }

        private ExtractionResult ProcessPix(Pix pix)
        {
            using var result = _engine.Process(pix);
            
            // Python's exact approach: reconstruct lines from word-level data
            var lines = new Dictionary<string, List<string>>();
            var confidences = new Dictionary<string, List<double>>();
            
            // Get word-level data like Python does
            using var iter = result.GetIterator();
            iter.Begin();
            
            do
            {
                var word = iter.GetText(Tesseract.PageIteratorLevel.Word);
                var conf = iter.GetConfidence(Tesseract.PageIteratorLevel.Word);
                
                if (!string.IsNullOrWhiteSpace(word))
                {
                    // Get block/paragraph/line info for grouping
                    iter.TryGetBoundingBox(Tesseract.PageIteratorLevel.Word, out var box);
                    var lineKey = $"{box.Y1}_{box.X1}"; // Simple grouping by position
                    
                    if (!lines.ContainsKey(lineKey))
                    {
                        lines[lineKey] = new List<string>();
                        confidences[lineKey] = new List<double>();
                    }
                    lines[lineKey].Add(word);
                    confidences[lineKey].Add(conf);
                }
            } while (iter.Next(Tesseract.PageIteratorLevel.Word));
            
            // Reconstruct text like Python does
            var reconstructedText = new List<string>();
            foreach (var kvp in lines)
            {
                var lineText = string.Join(" ", kvp.Value);
                reconstructedText.Add(lineText);
            }
            
            var fullText = string.Join("\n", reconstructedText);
            
            // Calculate average confidence like Python does
            var allConfs = confidences.Values.SelectMany(x => x).ToList();
            double avgConfidence = allConfs.Any() ? allConfs.Average() : 80.0;

            return new ExtractionResult
            {
                RawText = fullText ?? string.Empty,
                Confidence = avgConfidence,
                Tags = new List<TagItem>(),
                Equipment = new List<EquipmentItem>()
            };
        }

        private ExtractionResult CreateEmptyResult(string message)
        {
            return new ExtractionResult
            {
                RawText = string.Empty,
                Confidence = 0,
                Tags = new List<TagItem>(),
                Equipment = new List<EquipmentItem>()
            };
        }
        
        /// <summary>
        /// Dispose the Tesseract engine
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _engine?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
