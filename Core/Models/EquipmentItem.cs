namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents equipment extracted from the document
    /// </summary>
    public class EquipmentItem
    {
        public string Value { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int PageNumber { get; set; }
        public string SourceFile { get; set; } = string.Empty;
    }
}
