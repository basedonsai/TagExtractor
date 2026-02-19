using OCRTool.Core.Models;

namespace OCRTool.Core.Interfaces
{
    /// <summary>
    /// Interface for OCR providers
    /// This abstraction allows swapping OCR engines without changing business logic
    /// </summary>
    public interface IOCRProvider
    {
        /// <summary>
        /// Process an image and extract text
        /// </summary>
        /// <param name="imageData">Raw image bytes</param>
        /// <returns>Extraction result with text and confidence</returns>
        ExtractionResult ProcessImage(byte[] imageData);
    }
}
