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
    }
}
