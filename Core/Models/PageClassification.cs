namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents the classification result for a page based on layout token analysis.
    /// Contains the determined page type, reasoning for the classification decision,
    /// and row/column counts for table-structured pages.
    /// </summary>
    public class PageClassification
    {
        /// <summary>
        /// The classified layout type of the page (Table, Scattered, or Sparse).
        /// Determines which extraction strategy should be applied.
        /// </summary>
        public PageType PageType { get; set; }
        
        /// <summary>
        /// Human-readable explanation of why this classification was assigned.
        /// Used for logging and debugging classification decisions.
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of distinct row clusters detected in the page layout.
        /// Only populated for Table page types; 0 for Scattered and Sparse.
        /// </summary>
        public int RowCount { get; set; }
        
        /// <summary>
        /// Number of distinct column clusters detected in the page layout.
        /// Only populated for Table page types; 0 for Scattered and Sparse.
        /// </summary>
        public int ColumnCount { get; set; }
    }
}
