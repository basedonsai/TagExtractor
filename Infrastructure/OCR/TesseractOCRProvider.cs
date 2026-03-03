using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Tesseract;

namespace OCRTool.Infrastructure.OCR
{
    /// <summary>
    /// Industrial-grade Tesseract OCR Provider
    /// Stable for electrical drawings, GA layouts, panel schedules
    /// </summary>
    public class TesseractOCRProvider : IOCRProvider, IDisposable
    {
        private readonly TesseractEngine _engine;
        private readonly string _tessdataPath;
        private bool _disposed;

        public TesseractOCRProvider(string tessdataPath)
        {
            _tessdataPath = tessdataPath;
            _engine = new TesseractEngine(tessdataPath, "eng", EngineMode.LstmOnly);

            // Better for structured engineering text
            _engine.DefaultPageSegMode = PageSegMode.Auto;

            // Industrial tag whitelist
            _engine.SetVariable("tessedit_char_whitelist",
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-+./:");

            _engine.SetVariable("preserve_interword_spaces", "1");
        }

        /// <summary>
        /// Get TSV output from Tesseract for layout-aware extraction
        /// </summary>
        public string GetTsvOutput(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return string.Empty;

            string tempImagePath = null;
            string tempOutputBase = null;

            try
            {
                // Create temporary files
                tempImagePath = Path.Combine(Path.GetTempPath(), $"tesseract_input_{Guid.NewGuid()}.png");
                tempOutputBase = Path.Combine(Path.GetTempPath(), $"tesseract_output_{Guid.NewGuid()}");

                // Write image to temp file
                File.WriteAllBytes(tempImagePath, imageData);

                // Find tesseract executable
                string tesseractExe = FindTesseractExecutable();
                if (string.IsNullOrEmpty(tesseractExe))
                {
                    return string.Empty;
                }

                // Run tesseract with TSV output
                var startInfo = new ProcessStartInfo
                {
                    FileName = tesseractExe,
                    Arguments = $"\"{tempImagePath}\" \"{tempOutputBase}\" -l eng --psm 3 tsv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                // Set TESSDATA_PREFIX environment variable
                var tessdataParent = Directory.GetParent(_tessdataPath)?.FullName;
                if (!string.IsNullOrEmpty(tessdataParent))
                {
                    startInfo.EnvironmentVariables["TESSDATA_PREFIX"] = tessdataParent;
                }

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return string.Empty;

                    process.WaitForExit(30000); // 30 second timeout

                    // Read TSV output file
                    string tsvOutputPath = tempOutputBase + ".tsv";
                    if (File.Exists(tsvOutputPath))
                    {
                        return File.ReadAllText(tsvOutputPath, Encoding.UTF8);
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (tempImagePath != null && File.Exists(tempImagePath))
                        File.Delete(tempImagePath);

                    if (tempOutputBase != null)
                    {
                        var tsvFile = tempOutputBase + ".tsv";
                        if (File.Exists(tsvFile))
                            File.Delete(tsvFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Find tesseract executable in common locations
        /// </summary>
        private string FindTesseractExecutable()
        {
            var searchPaths = new[]
            {
                // Common installation paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "tesseract", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "tesseract", "tesseract.exe"),
                
                // Relative to tessdata path
                Path.Combine(Directory.GetParent(_tessdataPath)?.FullName ?? "", "tesseract.exe"),
                
                // Check PATH environment variable
                "tesseract.exe"
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Try to find in PATH
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "tesseract.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0 && File.Exists(lines[0]))
                            return lines[0];
                    }
                }
            }
            catch
            {
                // Ignore errors finding in PATH
            }

            return null;
        }

        /// <summary>
        /// Process image bytes
        /// </summary>
        public ExtractionResult ProcessImage(byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                    return CreateEmptyResult();

                try
                {
                    using var pix = Pix.LoadFromMemory(imageData);
                    return ProcessPix(pix);
                }
                catch
                {
                    return TryLoadFromTempFile(imageData);
                }
            }
            catch
            {
                return CreateEmptyResult();
            }
        }

        /// <summary>
        /// Fallback if memory load fails
        /// </summary>
        private ExtractionResult TryLoadFromTempFile(byte[] imageData)
        {
            try
            {
                var tempFile = Path.GetTempFileName();
                var tempPng = Path.ChangeExtension(tempFile, ".png");

                File.WriteAllBytes(tempPng, imageData);

                try
                {
                    using var pix = Pix.LoadFromFile(tempPng);
                    return ProcessPix(pix);
                }
                finally
                {
                    try { File.Delete(tempPng); } catch { }
                    try { File.Delete(tempFile); } catch { }
                }
            }
            catch
            {
                return CreateEmptyResult();
            }
        }

        /// <summary>
        /// Core OCR logic (CLEAN + STABLE)
        /// </summary>
        private ExtractionResult ProcessPix(Pix pix)
        {
            using var page = _engine.Process(pix);

            var rawText = page.GetText() ?? string.Empty;

            // Calculate word-level confidence properly
            double avgConfidence = 0;
            var confidences = new List<float>();

            using (var iter = page.GetIterator())
            {
                iter.Begin();

                do
                {
                    var conf = iter.GetConfidence(PageIteratorLevel.Word);
                    if (conf > 0)
                        confidences.Add(conf);
                }
                while (iter.Next(PageIteratorLevel.Word));
            }

            if (confidences.Any())
                avgConfidence = confidences.Average();
            else
                avgConfidence = page.GetMeanConfidence() * 100;

            return new ExtractionResult
            {
                RawText = rawText,
                Confidence = avgConfidence,
                Tags = new List<TagItem>(),
                Equipment = new List<EquipmentItem>()
            };
        }

        private ExtractionResult CreateEmptyResult()
        {
            return new ExtractionResult
            {
                RawText = string.Empty,
                Confidence = 0,
                Tags = new List<TagItem>(),
                Equipment = new List<EquipmentItem>()
            };
        }

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
                    _engine?.Dispose();

                _disposed = true;
            }
        }
    }
}
