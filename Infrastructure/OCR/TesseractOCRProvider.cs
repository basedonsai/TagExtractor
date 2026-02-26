using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool _disposed;

        public TesseractOCRProvider(string tessdataPath)
        {
            _engine = new TesseractEngine(tessdataPath, "eng", EngineMode.LstmOnly);

            // Better for structured engineering text
            _engine.DefaultPageSegMode = PageSegMode.Auto;

            // Industrial tag whitelist
            _engine.SetVariable("tessedit_char_whitelist",
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-+./:");

            _engine.SetVariable("preserve_interword_spaces", "1");
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
