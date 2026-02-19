using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OCRTool.Infrastructure.Logging
{
    /// <summary>
    /// File logger for processing operations
    /// Logs each page processed and its status
    /// </summary>
    public class FileLogger
    {
        private readonly string _logFolder;
        private readonly List<ProcessingLog> _logs;
        private readonly object _lock = new object();

        public FileLogger()
        {
            _logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _logs = new List<ProcessingLog>();

            // Create logs folder if it doesn't exist
            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }
        }

        /// <summary>
        /// Log a processing event
        /// </summary>
        public void Log(ProcessingLog entry)
        {
            lock (_lock)
            {
                _logs.Add(entry);
            }
        }

        /// <summary>
        /// Log a page processed
        /// </summary>
        public void LogPage(string fileName, int pageNumber, bool isSearchable, int itemsFound, string status, string message = "")
        {
            Log(new ProcessingLog
            {
                FileName = fileName,
                PageNumber = pageNumber,
                PageType = isSearchable ? "searchable" : "scanned",
                ItemsFound = itemsFound,
                Status = status,
                Message = message
            });
        }

        /// <summary>
        /// Get all logs
        /// </summary>
        public List<ProcessingLog> GetLogs()
        {
            lock (_lock)
            {
                return new List<ProcessingLog>(_logs);
            }
        }

        /// <summary>
        /// Save logs to a text file
        /// </summary>
        public string SaveToFile()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"ocr_processing_{timestamp}.log";
            var filePath = Path.Combine(_logFolder, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("OCR Processing Log");
            sb.AppendLine("===================");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            lock (_lock)
            {
                foreach (var log in _logs)
                {
                    sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {log.FileName} | Page {log.PageNumber} | {log.PageType} | Items: {log.ItemsFound} | {log.Status} | {log.Message}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }

        /// <summary>
        /// Clear all logs
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _logs.Clear();
            }
        }
    }
}
