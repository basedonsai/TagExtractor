using OCRTool.Core.Models;

namespace OCRTool.Core.Interfaces
{
    /// <summary>
    /// Interface for extracting layout tokens with spatial metadata from PDF and OCR sources
    /// </summary>
    public interface ILayoutTokenExtractor
    {
        /// <summary>
        /// Extract layout tokens from PdfPig page
        /// </summary>
        /// <param name="page">PdfPig page object containing text elements</param>
        /// <param name="pageNumber">Page number for token metadata</param>
        /// <returns>List of layout tokens with bounding boxes and spatial metadata</returns>
        List<LayoutToken> ExtractFromPdfPigPage(UglyToad.PdfPig.Content.Page page, int pageNumber);
        
        /// <summary>
        /// Extract layout tokens from Tesseract TSV output
        /// </summary>
        /// <param name="tsvOutput">TSV format output from Tesseract OCR</param>
        /// <param name="pageNumber">Page number for token metadata</param>
        /// <returns>List of layout tokens with bounding boxes and spatial metadata</returns>
        List<LayoutToken> ExtractFromTesseractTsv(string tsvOutput, int pageNumber);
        
        /// <summary>
        /// Normalize Tesseract coordinates to PdfPig coordinate system
        /// </summary>
        /// <param name="token">Layout token with Tesseract coordinates</param>
        /// <param name="pageWidth">Width of the page</param>
        /// <param name="pageHeight">Height of the page</param>
        /// <returns>Layout token with normalized coordinates in PdfPig coordinate system</returns>
        LayoutToken NormalizeCoordinates(LayoutToken token, double pageWidth, double pageHeight);
    }
}
