using ClosedXML.Excel;
using Intranet.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing;

namespace Intranet.Services
{
    public class RegisterService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public RegisterService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task AddToMonthlyRegisterAsync(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Requester).ThenInclude(u => u.Department)
                .Include(r => r.Documents)
                .Include(r => r.Quotes.Where(q => q.IsSelected))
                .FirstOrDefaultAsync(r => r.Id == requestId);

            var verification = await _context.AiVerifications.FirstOrDefaultAsync(v => v.RequestId == requestId);

            if (request == null) return;

            
            string folder = Path.Combine(_env.WebRootPath, "registers");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = $"Procurement_Register_{DateTime.Now:MMMM_yyyy}.xlsx";
            string filePath = Path.Combine(folder, fileName);

            // 2. Load or Create Workbook
            using (var workbook = File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.Add("Register");

                // 3. Create Headers if New File
                if (worksheet.LastRowUsed() == null)
                {
                    var header = worksheet.Row(1);
                    header.Cell(1).Value = "Date Closed";
                    header.Cell(2).Value = "Request ID";
                    header.Cell(3).Value = "Requester";
                    header.Cell(4).Value = "Department";
                    header.Cell(5).Value = "Description / Items";
                    header.Cell(6).Value = "Supplier";
                    header.Cell(7).Value = "Amount (Excl VAT)";
                    header.Cell(8).Value = "Invoice Number";
                    header.Style.Font.Bold = true;
                    header.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // 4. Append Data
                int nextRow = worksheet.LastRowUsed().RowNumber() + 1;
                var invoiceDoc = request.Documents.FirstOrDefault(d => d.DocType == "Invoice");
                var winningQuote = request.Quotes.FirstOrDefault();

                worksheet.Cell(nextRow, 1).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(nextRow, 2).Value = request.Id;
                worksheet.Cell(nextRow, 3).Value = $"{request.Requester.FirstName} {request.Requester.Surname}";
                worksheet.Cell(nextRow, 4).Value = request.Requester.Department?.Name ?? "N/A";
                worksheet.Cell(nextRow, 5).Value = request.Description;
                worksheet.Cell(nextRow, 6).Value = winningQuote?.SupplierName ?? "N/A";
                worksheet.Cell(nextRow, 7).Value = request.TotalAmount;
                worksheet.Cell(nextRow, 8).Value = verification?.InvoiceNumber ?? "N/A";

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }
    }
}