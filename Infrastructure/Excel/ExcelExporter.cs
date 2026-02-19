using ClosedXML.Excel;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace OCRTool.Infrastructure.Excel
{
    /// <summary>
    /// Exports extraction results to Excel
    /// Creates separate sheets for Tags, Equipment, and Processing Log
    /// </summary>
    public class ExcelExporter
    {
        /// <summary>
        /// Export results to Excel file - Python format (only Tags and Equipment sheets)
        /// </summary>
        public void Export(string outputPath, List<ExtractionResult> results, List<ProcessingLog> logs)
        {
            using var workbook = new XLWorkbook();

            // Add tags sheet
            AddTagsSheet(workbook, results);

            // Add equipment sheet
            AddEquipmentSheet(workbook, results);

            // Save the workbook
            workbook.SaveAs(outputPath);
        }

        /// <summary>
        /// Export with default empty log
        /// </summary>
        public void Export(string outputPath, List<ExtractionResult> results)
        {
            Export(outputPath, results, new List<ProcessingLog>());
        }

        private void AddSummarySheet(XLWorkbook workbook, List<ExtractionResult> results)
        {
            var sheet = workbook.Worksheets.Add("Summary");

            // Title
            sheet.Cell(1, 1).Value = "OCR Extraction Summary";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;

            // Statistics
            int row = 3;
            sheet.Cell(row, 1).Value = "Total Files Processed:";
            sheet.Cell(row, 2).Value = results.Count;
            row++;

            sheet.Cell(row, 1).Value = "Total Tags Found:";
            sheet.Cell(row, 2).Value = results.Sum(r => r.Tags.Count);
            row++;

            sheet.Cell(row, 1).Value = "Total Equipment Found:";
            sheet.Cell(row, 2).Value = results.Sum(r => r.Equipment.Count);
            row++;

            sheet.Cell(row, 1).Value = "Generated:";
            sheet.Cell(row, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }

        private void AddTagsSheet(XLWorkbook workbook, List<ExtractionResult> results)
        {
            var sheet = workbook.Worksheets.Add("Tags");

            // Headers - Python format
            sheet.Cell(1, 1).Value = "source_file";
            sheet.Cell(1, 2).Value = "page";
            sheet.Cell(1, 3).Value = "type";
            sheet.Cell(1, 4).Value = "value";
            sheet.Cell(1, 5).Value = "confidence";

            // Data
            int row = 2;
            foreach (var result in results)
            {
                foreach (var tag in result.Tags)
                {
                    sheet.Cell(row, 1).Value = result.SourceFile;
                    sheet.Cell(row, 2).Value = result.PageNumber;
                    sheet.Cell(row, 3).Value = tag.Type;
                    sheet.Cell(row, 4).Value = tag.Value;
                    sheet.Cell(row, 5).Value = tag.Confidence;
                    row++;
                }
            }

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }

        private void AddEquipmentSheet(XLWorkbook workbook, List<ExtractionResult> results)
        {
            var sheet = workbook.Worksheets.Add("Equipment");

            // Headers - Python format
            sheet.Cell(1, 1).Value = "source_file";
            sheet.Cell(1, 2).Value = "page";
            sheet.Cell(1, 3).Value = "type";
            sheet.Cell(1, 4).Value = "value";
            sheet.Cell(1, 5).Value = "confidence";

            // Data
            int row = 2;
            foreach (var result in results)
            {
                foreach (var equip in result.Equipment)
                {
                    sheet.Cell(row, 1).Value = result.SourceFile;
                    sheet.Cell(row, 2).Value = result.PageNumber;
                    sheet.Cell(row, 3).Value = "EQUIP";
                    sheet.Cell(row, 4).Value = equip.Value;
                    sheet.Cell(row, 5).Value = equip.Confidence;
                    row++;
                }
            }

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }

        private void AddLogSheet(XLWorkbook workbook, List<ProcessingLog> logs)
        {
            var sheet = workbook.Worksheets.Add("Processing Log");

            // Headers
            sheet.Cell(1, 1).Value = "Timestamp";
            sheet.Cell(1, 2).Value = "File";
            sheet.Cell(1, 3).Value = "Page";
            sheet.Cell(1, 4).Value = "Type";
            sheet.Cell(1, 5).Value = "Items";
            sheet.Cell(1, 6).Value = "Status";
            sheet.Cell(1, 7).Value = "Message";

            // Style headers
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightYellow;

            // Data
            int row = 2;
            foreach (var log in logs)
            {
                sheet.Cell(row, 1).Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                sheet.Cell(row, 2).Value = log.FileName;
                sheet.Cell(row, 3).Value = log.PageNumber;
                sheet.Cell(row, 4).Value = log.PageType;
                sheet.Cell(row, 5).Value = log.ItemsFound;
                sheet.Cell(row, 6).Value = log.Status;
                sheet.Cell(row, 7).Value = log.Message;
                row++;
            }

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }
    }
}
