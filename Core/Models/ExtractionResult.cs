using System.Collections.Generic;
namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents the result of extracting items from a document page
    /// </summary>
    public class ExtractionResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public bool IsSearchable { get; set; }
        public List<TagItem> Tags { get; set; } = new List<TagItem>();
        public List<EquipmentItem> Equipment { get; set; } = new List<EquipmentItem>();
        public double Confidence { get; set; }
        public string RawText { get; set; } = string.Empty;
        /// <summary>
        /// Collection of layout tokens with spatial metadata extracted from the page.
        /// Initialized to empty list, never null.
        /// </summary>
        public List<LayoutToken> LayoutTokens { get; set; } = new List<LayoutToken>();
        /// <summary>
        /// Page classification result based on layout token analysis.
        /// Nullable to support pages where classification hasn't been performed yet.
        /// </summary>
        public PageClassification? Classification { get; set; }
        /// <summary>
        /// Collection of structured records extracted from the page using layout-aware processing.
        /// Records are built by TableEngine (for table layouts) or ProximityEngine (for scattered layouts).
        /// Initialized to empty list, never null.
        /// </summary>
        public List<StructuredRecord> StructuredRecords { get; set; } = new List<StructuredRecord>();
    }
}