namespace OCRTool.Core.Models
{
    /// Represents a tag extracted from the document
    public class TagItem
    {
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int PageNumber { get; set; }
        public string SourceFile { get; set; } = string.Empty;
    }
}

