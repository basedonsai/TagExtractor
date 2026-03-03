namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents an extracted record with source tracking.
    /// Contains tag, equipment, rating, and description fields extracted from industrial documents,
    /// along with metadata about the source file, page number, and extraction method used.
    /// </summary>
    public class StructuredRecord
    {
        /// <summary>
        /// Tag identifier extracted from the document (e.g., equipment tag numbers, instrument identifiers).
        /// </summary>
        public string Tag { get; set; } = string.Empty;
        
        /// <summary>
        /// Equipment name or description extracted from the document.
        /// </summary>
        public string Equipment { get; set; } = string.Empty;
        
        /// <summary>
        /// Technical specifications or ratings associated with the equipment.
        /// </summary>
        public string Rating { get; set; } = string.Empty;
        
        /// <summary>
        /// Additional description or notes about the equipment.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Source file name from which this record was extracted.
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// Page number within the source document where this record was found.
        /// </summary>
        public int Page { get; set; }
        
        /// <summary>
        /// Extraction method used to build this record (TableEngine, ProximityEngine, or RegexSystem).
        /// </summary>
        public ExtractionMethod Method { get; set; }
    }
}
