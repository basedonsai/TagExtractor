using ClosedXML.Excel;
using OCRTool.Core.Models;
using System;
using System.Collections.Generic;

namespace OCRTool.Infrastructure.Excel
{
    public class ExcelExporter
    {
        public void Export(string outputPath, List<ExtractionResult> results, List<ProcessingLog> logs)
        {
            using var workbook = new XLWorkbook();

            AddTagsSheet(workbook, results);
            AddEquipmentSheet(workbook, results);


            workbook.SaveAs(outputPath);
        }

        public void Export(string outputPath, List<ExtractionResult> results)
        {
            Export(outputPath, results, new List<ProcessingLog>());
        }

        private void AddTagsSheet(XLWorkbook workbook, List<ExtractionResult> results)
        {
            var sheet = workbook.Worksheets.Add("Tags");

            sheet.Cell(1, 1).Value = "source_file";
            sheet.Cell(1, 2).Value = "page";
            sheet.Cell(1, 3).Value = "type";
            sheet.Cell(1, 4).Value = "value";

            int row = 2;
            var seen = new HashSet<string>();

            foreach (var result in results)
            {
                foreach (var tag in result.Tags)
                {
                    if (seen.Contains(tag.Value))
                        continue;

                    seen.Add(tag.Value);

                    sheet.Cell(row, 1).Value = result.SourceFile;
                    sheet.Cell(row, 2).Value = result.PageNumber;
                    sheet.Cell(row, 3).Value = tag.Type;
                    sheet.Cell(row, 4).Value = tag.Value;
                    row++;
                }
            }

            sheet.Columns().AdjustToContents();
        }

        private void AddEquipmentSheet(XLWorkbook workbook, List<ExtractionResult> results)
        {
            var sheet = workbook.Worksheets.Add("Equipment");

            sheet.Cell(1, 1).Value = "source_file";
            sheet.Cell(1, 2).Value = "page";
            sheet.Cell(1, 3).Value = "type";
            sheet.Cell(1, 4).Value = "value";

            int row = 2;
            var seen = new HashSet<string>();

            foreach (var result in results)
            {
                foreach (var equip in result.Equipment)
                {
                    if (seen.Contains(equip.Value))
                        continue;

                    seen.Add(equip.Value);

                    sheet.Cell(row, 1).Value = result.SourceFile;
                    sheet.Cell(row, 2).Value = result.PageNumber;
                    sheet.Cell(row, 3).Value = "EQUIP";
                    sheet.Cell(row, 4).Value = equip.Value;
                    row++;
                }
            }

            sheet.Columns().AdjustToContents();
        }

    }
}