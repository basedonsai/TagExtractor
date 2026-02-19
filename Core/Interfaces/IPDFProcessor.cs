using System.Collections.Generic;
using OCRTool.Core.Models;

namespace OCRTool.Core.Interfaces
{
    /// <summary>
    /// Interface for PDF processors
    /// </summary>
    public interface IPDFProcessor
    {
        /// <summary>
        /// Extract pages from a PDF file
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file</param>
        /// <returns>List of page results</returns>
        List<PageResult> ExtractPages(string pdfPath);
    }

    /// <summary>
    /// Represents the result of processing a single page
    /// </summary>
    public class PageResult
    {
        public int PageNumber { get; set; }
        public bool IsSearchable { get; set; }
        public string RawText { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; }
        public int TextLength { get; set; }
    }
}
