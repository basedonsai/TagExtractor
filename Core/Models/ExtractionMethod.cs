namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents the extraction method used to build a structured record.
    /// Used to track which extraction engine produced each record for traceability and analysis.
    /// </summary>
    public enum ExtractionMethod
    {
        /// <summary>
        /// Record was extracted using the Table Engine, which processes table-like layouts
        /// by clustering rows and columns. Applied to pages classified as Table type.
        /// </summary>
        TableEngine,
        
        /// <summary>
        /// Record was extracted using the Proximity Engine, which processes scattered layouts
        /// by grouping nearby elements based on spatial distance. Applied to pages classified as Scattered type.
        /// </summary>
        ProximityEngine,
        
        /// <summary>
        /// Record was extracted using the Regex System, which uses pattern matching on normalized text.
        /// Applied as a fallback when layout-aware processing fails or to Sparse pages.
        /// </summary>
        RegexSystem
    }
}
