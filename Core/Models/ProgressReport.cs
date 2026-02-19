using System;

namespace OCRTool.Core.Models
{
    /// <summary>
    /// Progress report for batch processing updates
    /// </summary>
    public class ProgressReport
    {
        public int FileNumber { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int PercentComplete { get; set; }
    }
}
