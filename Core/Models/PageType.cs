namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents the layout classification of a page based on spatial token distribution.
    /// Used by the page classifier to determine appropriate extraction strategies.
    /// </summary>
    public enum PageType
    {
        /// <summary>
        /// Page has regular row and column alignment, typical of equipment schedules and structured lists.
        /// Indicates that table-based extraction with row/column clustering should be applied.
        /// </summary>
        Table,
        
        /// <summary>
        /// Page has irregular token distribution without clear alignment, typical of P&ID drawings.
        /// Indicates that proximity-based extraction with spatial grouping should be applied.
        /// </summary>
        Scattered,
        
        /// <summary>
        /// Page has fewer than 10 tokens, typical of cover pages, blank pages, or pages with minimal content.
        /// Indicates that structured extraction should be skipped in favor of regex-only processing.
        /// </summary>
        Sparse
    }
}
