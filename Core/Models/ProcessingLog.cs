using System;

namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents a log entry for processing
    /// </summary>
    public class ProcessingLog
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string FileName { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string PageType { get; set; } = string.Empty; // "scanned" or "searchable"
        public int ItemsFound { get; set; }
        public string Status { get; set; } = string.Empty; // "success", "warning", "error"
        public string Message { get; set; } = string.Empty;
    }
}
