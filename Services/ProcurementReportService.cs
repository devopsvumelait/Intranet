using Intranet.Models;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace Intranet.Services
{
    public class ProcurementReportService
    {
        private readonly AppDbContext _context;

        public ProcurementReportService(AppDbContext context)
        {
            _context = context;
        }
        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        public IQueryable<Request> BuildBaseQuery(Guid userId, bool isMD, bool isFinance, bool isHOS, bool isHOO)
        {
            IQueryable<Request> query = _context.Requests.Include(r => r.Requester);

            if (isMD) { /* View All */ }
            else if (isFinance)
            {
                var financeStatuses = new[] { "Awaiting_Payment", "Awaiting_Verification", "Awaiting_Invoice", "Closed" };
                query = query.Where(r => financeStatuses.Contains(r.Status));
            }
            else if (isHOS)
            {
                var hosStatuses = new[] { "Pending_HOS", "Awaiting_Payment", "Awaiting_Verification", "Closed" };
                query = query.Where(r => hosStatuses.Contains(r.Status));
            }
            else if (isHOO)
            {
                var hooStatuses = new[] { "Pending_HOO", "Awaiting_Payment", "Awaiting_Verification", "Closed" };
                query = query.Where(r => hooStatuses.Contains(r.Status));
            }
            else
            {
                query = query.Where(r => r.RequesterId == userId);
            }

            return query;
        }

        public byte[] GenerateExcel(List<Request> data)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Procurement Report");

                // Header Styling
                var headers = new[] { "Request ID", "Requester", "Supplier / Vendor", "Description",
            "Cost Type", "Amount", "Status", "Date Created",
            "Quote Type", "Customer Name", "Department Type" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(row, 1).Value = item.Id;
                    worksheet.Cell(row, 2).Value = $"{item.Requester?.FirstName} {item.Requester?.Surname}";
                    worksheet.Cell(row, 3).Value = item.Quotes?.FirstOrDefault(q => q.IsSelected)?.SupplierName ?? "N/A";
                    worksheet.Cell(row, 4).Value = item.Description;
                    worksheet.Cell(row, 5).Value = item.CostType;
                    worksheet.Cell(row, 6).Value = item.TotalAmount;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "R #,##0.00";
                    worksheet.Cell(row, 7).Value = item.Status;
                    worksheet.Cell(row, 8).Value = item.CreatedAt?.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 9).Value = item.QuoteType;
                    worksheet.Cell(row, 10).Value = item.CustomerName;
                    worksheet.Cell(row, 11).Value = item.DepartmentType;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<List<Request>> GetReportDataForUser(User user)
        {
            IQueryable<Request> query = _context.Requests
                .Include(r => r.Requester)
                    .ThenInclude(u => u.Department)
                .Include(r => r.Quotes)
                .Where(r => r.Id != -1);

            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            bool isMD = userRoles.Contains("MD");
            bool isFinance = userRoles.Contains("Finance");
            bool isHOS = userRoles.Contains("HOS");
            bool isHOO = userRoles.Contains("HOO");

            if (!isMD)
            {
                if (isFinance)
                {
                    var statuses = new[] { "Awaiting_Payment", "Awaiting_Verification", "Awaiting_Invoice", "Closed" };
                    query = query.Where(r => statuses.Contains(r.Status));
                }
                else if (isHOS)
                {
                    var statuses = new[] { "Pending_HOS", "Awaiting_Payment", "Awaiting_Verification", "Closed" };
                    query = query.Where(r => statuses.Contains(r.Status));
                }
                else if (isHOO)
                {
                    var statuses = new[] { "Pending_HOO", "Awaiting_Payment", "Awaiting_Verification", "Closed" };
                    query = query.Where(r => statuses.Contains(r.Status));
                }
                else
                {
                    query = query.Where(r => r.RequesterId == user.Id);
                }
            }

            var lastMonth = GetSouthAfricanTime().AddMonths(-1);
            return await query
                .Where(r => r.CreatedAt >= lastMonth)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}