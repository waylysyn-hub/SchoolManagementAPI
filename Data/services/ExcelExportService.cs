using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace Data.Services
{
    public class ExcelExportService
    {
        /// <summary>
        /// Generic method to export any list of objects to Excel.
        /// </summary>
        /// <typeparam name="T">Type of objects to export</typeparam>
        /// <param name="data">List of data</param>
        /// <param name="headers">List of headers (columns) with property names</param>
        /// <param name="sheetName">Sheet name</param>
        /// <returns>MemoryStream of Excel file</returns>
        public static MemoryStream ExportToExcel<T>(List<T> data, List<(string Header, Func<T, object> ValueSelector)> headers, string sheetName)
        {
            if (data == null) data = new List<T>();
            if (headers == null || headers.Count == 0) throw new ArgumentException("Headers must be provided");

            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(sheetName);

            // Header row formatting
            for (int i = 0; i < headers.Count; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i].Header;
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.AshGrey;
                ws.Cell(1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Fill data
            for (int row = 0; row < data.Count; row++)
            {
                for (int col = 0; col < headers.Count; col++)
                {
                    var value = headers[col].ValueSelector(data[row]);
                    ws.Cell(row + 2, col + 1).Value = value?.ToString() ?? "";
                }
            }


            // Adjust columns width
            ws.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
