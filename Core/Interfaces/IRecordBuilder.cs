using OCRTool.Core.Models;

namespace OCRTool.Core.Interfaces
{
    /// <summary>
    /// Interface for building structured records from layout tokens.
    /// Implemented by TableEngine (for table-like layouts) and ProximityEngine (for scattered layouts).
    /// </summary>
    public interface IRecordBuilder
    {
        /// <summary>
        /// Build structured records from layout tokens.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens extracted from a page</param>
        /// <param name="pageNumber">Page number within the source document</param>
        /// <param name="sourceFile">Source file name</param>
        /// <returns>List of structured records extracted from the layout tokens</returns>
        List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile);
        
        /// <summary>
        /// Check if this builder can process the given page type.
        /// </summary>
        /// <param name="pageType">The page type classification (Table, Scattered, or Sparse)</param>
        /// <returns>True if this builder can process the page type, false otherwise</returns>
        bool CanProcess(PageType pageType);
    }
}
