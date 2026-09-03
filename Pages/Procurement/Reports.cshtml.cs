using ClosedXML.Excel;
using Intranet.Models;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement
{
    [Authorize(Roles = "Manager, Finance, HOO, HOS, MD")]
    public class ReportsModel : PageModel
    {
        private readonly AppDbContext _context;
        public ReportsModel(AppDbContext context) => _context = context;

        public Dictionary<string, List<Request>> Tabs { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public int? SelectedDeptId { get; set; }
        public string? CostType { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DepartmentFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CostTypeFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? RequestTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)] public string? QuoteTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public int AcceptedWaitingOnFinance { get; set; }
        public int Pending { get; set; }
        public int Denied { get; set; }
        public int PaidPendingOnManager { get; set; }
        public int PendingOnMD { get; set; }
        public int Closed { get; set; }

        public int AvgApprovalDays { get; set; }
        public int RequestsThisMonth { get; set; }
        public int OpenRequests { get; set; }

        public List<DepartmentSpendDto> DepartmentSpend { get; set; } = new();
        public List<VendorPerformanceDto> VendorPerformance { get; set; } = new();

        public List<AuditTrailDto> AuditTrail { get; set; } = new();

        public List<BudgetTrendDto> BudgetTrend { get; set; } = new();

        public decimal ForecastNextMonthSpend { get; set; }
        public List<SupplierSpendDto> TopSuppliers { get; set; } = new();
        public List<BudgetForecastDto> BudgetForecast { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public bool CanViewHighValueMDQueue { get; set; }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }
        public async Task OnGetAsync(
        int? deptId,
        string? costType,
        DateTime? startDate,
        DateTime? endDate)
        {
            try
            {
                SelectedDeptId = deptId;
                CostType = costType;

                if (!startDate.HasValue && !endDate.HasValue)
                {
                    startDate = DateTime.Today.AddMonths(-3);
                    endDate = DateTime.Today;
                }

                StartDate = startDate;
                EndDate = endDate;

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return;

                Departments = await _context.Departments.ToListAsync();

                // 1. Identity & Hardcoded Email Checks
                string[] specialViewerEmails = new[]
                {
                "sivashni.moodley@vumelait.co.za", // Replace with actual email 1
                "verndell.khan@vumelait.co.za", // Replace with actual email 2
                "sandika.sewnarain@vumelait.co.za"  // Replace with actual email 3
            };
                string currentUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;

                bool isActualMD = User.IsInRole("MD");
                bool isSpecialViewer = specialViewerEmails.Contains(currentUserEmail);
                bool isFinance = User.IsInRole("Finance");
                bool isHOS = User.IsInRole("HOS");
                bool isHOO = User.IsInRole("HOO");

                CanViewHighValueMDQueue = isActualMD;

                // 2. Build Base Data with Additive / Threshold Rules
                var query = BuildFilteredQuery(userId, deptId, costType, startDate, endDate,
                                            StatusFilter, DepartmentFilter, RequestTypeFilter,
                                            QuoteTypeFilter);

                
                if (isActualMD)
                {
                    
                }
                else if (isSpecialViewer)
                {
                    
                    query = query.Where(r => !(r.Status == "Pending_MD" && r.TotalAmount > 15000));
                }
                else
                {
                    
                    var allowedStatuses = new List<string>();

                    if (isHOO) allowedStatuses.Add("Pending_HOO");
                    if (isHOS) allowedStatuses.Add("Pending_HOS");
                    if (isFinance) allowedStatuses.AddRange(new[] { "AcceptedWaitingOnFinance", "Awaiting_Payment", "PO_Payment_Queue", "PO_Upload", "Awaiting_Verification" });

                    if (allowedStatuses.Any())
                    {
                        query = query.Where(r => allowedStatuses.Contains(r.Status) || r.RequesterId == userId);
                    }
                    else
                    {
                        query = query.Where(r => r.RequesterId == userId);
                    }
                }

                var data = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                // 1. Define the master list
                var allDepartments = new List<string> { "EUC Central", "EUC Regional", "Networks", "Cabling", "ADHOC", "Head Office", "Interns" };

                // 2. Determine which departments to display
                var departmentsToDisplay = !string.IsNullOrEmpty(DepartmentFilter)
                    ? new List<string> { DepartmentFilter }
                    : allDepartments;

                // 3. Group the filtered data
                var actualSpendData = data
                    .GroupBy(x => x.DepartmentType)
                    .ToDictionary(g => g.Key ?? "Unknown", g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) });

                // 4. Map to DTOs
                DepartmentSpend = departmentsToDisplay.Select(deptName => new DepartmentSpendDto
                {
                    Department = deptName,
                    RequestCount = actualSpendData.ContainsKey(deptName) ? actualSpendData[deptName].Count : 0,
                    TotalSpend = actualSpendData.ContainsKey(deptName) ? actualSpendData[deptName].Total : 0
                })
                .OrderByDescending(x => x.TotalSpend)
                .ToList();

                
                VendorPerformance = data
                    .Select(x => new
                    {
                        Request = x,
                        SelectedQuote = x.Quotes?.FirstOrDefault(q => q.IsSelected)
                    })
                    .Where(x => x.SelectedQuote != null)
                    .GroupBy(x => x.SelectedQuote.SupplierName ?? "Unknown")
                    .Select(g => new VendorPerformanceDto
                    {
                        Vendor = g.Key ?? "Unknown",
                        Requests = g.Count(),
                        TotalSpend = g.Sum(x => x.Request.TotalAmount),
                        AvgRequestValue = g.Average(x => x.Request.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalSpend)
                    .ToList();

                AuditTrail = await _context.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .Take(200)
                    .Select(a => new AuditTrailDto
                    {
                        Id = a.Id,
                        TableName = a.TableName,
                        RecordId = a.RecordId,
                        ActionType = a.ActionType,
                        Timestamp = a.Timestamp,
                        UserName = a.ActionBy.ToString()
                    })
                    .ToListAsync();

                BudgetTrend = data
                    .Where(x => x.CreatedAt != null)
                    .GroupBy(x => new { x.CreatedAt.Value.Year, x.CreatedAt.Value.Month })
                    .Select(g => new BudgetTrendDto
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        TotalSpend = g.Sum(x => x.TotalAmount),
                        RequestCount = g.Count()
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                TopSuppliers = data
                    .Select(x => new
                    {
                        Supplier = x.Quotes?.FirstOrDefault(q => q.IsSelected)?.SupplierName ?? "Unknown",
                        Amount = x.TotalAmount
                    })
                    .GroupBy(x => x.Supplier)
                    .Select(g => new SupplierSpendDto
                    {
                        Supplier = g.Key,
                        TotalSpend = g.Sum(x => x.Amount)
                    })
                    .OrderByDescending(x => x.TotalSpend)
                    .Take(5)
                    .ToList();

                var monthly = data
                    .Where(x => x.CreatedAt != null)
                    .GroupBy(x => new { x.CreatedAt.Value.Year, x.CreatedAt.Value.Month })
                    .Select(g => new
                    {
                        Period = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Total = g.Sum(x => x.TotalAmount)
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                decimal forecast = 0;
                if (monthly.Count >= 3)
                {
                    forecast = monthly.TakeLast(3).Average(x => x.Total);
                }
                else if (monthly.Any())
                {
                    forecast = monthly.Average(x => x.Total);
                }

                ForecastNextMonthSpend = forecast;

                BudgetForecast = monthly
                    .Select(x => new BudgetForecastDto
                    {
                        Period = x.Period.ToString("yyyy-MM"),
                        Actual = x.Total,
                        Forecast = forecast
                    })
                    .ToList();

                // Shared definition arrays
                var financeStatuses = new[] { "Awaiting_Payment", "PO_Payment_Queue", "PO_Upload", "Awaiting_Verification" };
                var managerReviewStatuses = new[] { "Awaiting_Invoice", "PO_Issued", "Awaiting_Manager_Closure" };

                
                AcceptedWaitingOnFinance = data.Count(r => financeStatuses.Contains(r.Status));

                if (isFinance)
                {
                    Pending = data.Count(r => financeStatuses.Contains(r.Status) || managerReviewStatuses.Contains(r.Status));
                }
                else
                {
                    Pending = data.Count(r => r.Status == "Pending_HOS" || r.Status == "Pending_HOO" || r.Status == "Pending_MD");
                }

                Denied = data.Count(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged");
                PaidPendingOnManager = data.Count(r => managerReviewStatuses.Contains(r.Status));
                PendingOnMD = data.Count(r => r.Status == "Pending_MD");
                Closed = data.Count(r => r.Status == "Closed");

                
                Tabs["All"] = data;

                if (isActualMD || isSpecialViewer)
                {
                    Tabs["PendingOnMD"] = data.Where(r => r.Status == "Pending_MD").ToList();
                    Tabs["AcceptedWaitingOnFinance"] = data.Where(r => financeStatuses.Contains(r.Status)).ToList();
                    Tabs["PendingOnHead"] = data.Where(r => r.Status == "Pending_HOS" || r.Status == "Pending_HOO").ToList();
                    Tabs["PaidPendingOnManager"] = data.Where(r => managerReviewStatuses.Contains(r.Status)).ToList();
                    Tabs["Denied"] = data.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged").ToList();
                    Tabs["Closed"] = data.Where(r => r.Status == "Closed").ToList();
                }
                else
                {
                    // Build tabs dynamically based on user's active permissions (Additive)
                    var userRequests = data;
                    Tabs["PendingOnMD"] = userRequests.Where(r => r.Status == "Pending_MD").ToList();
                    Tabs["AcceptedWaitingOnFinance"] = userRequests.Where(r => financeStatuses.Contains(r.Status)).ToList();
                    Tabs["PendingOnHead"] = userRequests.Where(r => r.Status == "Pending_HOS" || r.Status == "Pending_HOO").ToList();
                    Tabs["PaidPendingOnManager"] = userRequests.Where(r => managerReviewStatuses.Contains(r.Status)).ToList();
                    Tabs["Denied"] = userRequests.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged").ToList();
                    Tabs["Closed"] = userRequests.Where(r => r.Status == "Closed").ToList();
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An unexpected system routing trace execution fault occurred.");
            }
        }

        private IQueryable<Request> BuildFilteredQuery(Guid userId, int? deptId, string? costType, DateTime? startDate, DateTime? endDate, string? statusFilter, string? deptFilter, string? reqType, string? quoteType)
        {
            try
            {
                var query = _context.Requests
                    .Include(r => r.Requester).ThenInclude(u => u.Department)
                    .Include(r => r.Quotes)
                    .Where(r => r.Id != -1)
                    .AsQueryable();

                bool isMD = User.IsInRole("MD");
                bool isFinance = User.IsInRole("Finance");
                bool isHOS = User.IsInRole("HOS");
                bool isHOO = User.IsInRole("HOO");

                if (isMD) { }
                else if (isFinance)
                {
                    query = query.Where(r => r.Status == "Awaiting_Payment" || r.Status == "PO_Payment_Queue" || r.Status == "PO_Upload" || r.Status == "PO_Issued" || r.Status == "Awaiting_Manager_Closure" || r.Status == "Awaiting_Verification" || r.Status == "Awaiting_Invoice" || r.Status == "Closed" || r.Status == "Rejected_Acknowledged");
                }
                else if (isHOS)
                {
                    query = query.Where(r => r.Status == "Pending_HOS" || r.Status == "Pending_MD" || r.Status == "Awaiting_Payment" || r.Status == "PO_Payment_Queue" || r.Status == "PO_Upload" || r.Status == "PO_Issued" || r.Status == "Awaiting_Manager_Closure" || r.Status == "Awaiting_Verification" || r.Status == "Closed" || r.Status == "Rejected_Acknowledged");
                }
                else if (isHOO)
                {
                    query = query.Where(r => r.Status == "Pending_HOO" || r.Status == "Awaiting_Payment" || r.Status == "PO_Payment_Queue" || r.Status == "PO_Upload" || r.Status == "PO_Issued" || r.Status == "Awaiting_Manager_Closure" || r.Status == "Awaiting_Verification" || r.Status == "Closed" || r.Status == "Rejected_Acknowledged");
                }
                else
                {
                    query = query.Where(r => r.RequesterId == userId || r.Status == "Pending_MD");
                }

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    if (statusFilter == "Pending") query = query.Where(r => new[] { "Awaiting_Payment", "Awaiting_Invoice", "Awaiting_Verification", "PO_Issued", "Awaiting_Manager_Closure", "Pending_HOO", "Pending_HOS", "Pending_MD" }.Contains(r.Status));
                    else if (statusFilter == "Rejected") query = query.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged");
                    else query = query.Where(r => r.Status == statusFilter);
                }
                if (!string.IsNullOrEmpty(deptFilter)) query = query.Where(r => r.DepartmentType == deptFilter);
                if (!string.IsNullOrEmpty(reqType)) query = query.Where(r => r.RequestType == reqType);
                if (!string.IsNullOrEmpty(quoteType)) query = query.Where(r => r.QuoteType == quoteType);
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    string searchLower = SearchTerm.Trim().ToLower();
                    query = query.Where(r =>
                        r.Id.ToString().Contains(searchLower) ||
                        r.Description.ToLower().Contains(searchLower) ||
                        r.Quotes.Any(q => q.IsSelected && q.SupplierName.ToLower().Contains(searchLower)) ||
                        r.CustomerName.ToLower().Contains(searchLower));

                }
                // if (!string.IsNullOrEmpty(costTypeFilter)) query = query.Where(r => r.CostType == costTypeFilter);

                if (!string.IsNullOrEmpty(costType)) { query = query.Where(r => r.CostType == costType); }
                if (isMD && deptId.HasValue) { query = query.Where(r => r.Requester.DepartmentId == deptId.Value); }

                if (startDate.HasValue) { query = query.Where(r => r.CreatedAt >= startDate.Value); }
                if (endDate.HasValue)
                {
                    var inclusiveEnd = endDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.CreatedAt < inclusiveEnd);
                }

                return query;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected system routing trace execution fault occurred.");
                return Enumerable.Empty<Request>().AsQueryable();
            }
        }

        public async Task<IActionResult> OnGetExportAsync(
            string? tab,
            int? deptId,
            string? costType,
            DateTime? startDate,
            DateTime? endDate, string? statusFilter,
    string? deptFilter,
    string? reqType,
    string? quoteType, string? searchTerm)
        {
            try
            {
                if (!startDate.HasValue && !endDate.HasValue)
                {
                    startDate = GetSouthAfricanTime().Date.AddMonths(-3);
                    endDate = GetSouthAfricanTime().Date;
                }

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid.TryParse(userIdString, out Guid userId);

                var data = await BuildFilteredQuery(userId, deptId, costType, startDate, endDate,
                                                statusFilter, deptFilter, reqType, quoteType).ToListAsync();

                var financeStatuses = new[] { "Awaiting_Payment", "PO_Payment_Queue", "PO_Upload", "Awaiting_Verification" };
                var managerReviewStatuses = new[] { "Awaiting_Invoice", "PO_Issued" };

                this.SearchTerm = searchTerm;

                data = tab switch
                {
                    "PendingOnMD" => data.Where(r => r.Status == "Pending_MD").ToList(),
                    "AcceptedWaitingOnFinance" => data.Where(r => financeStatuses.Contains(r.Status)).ToList(),
                    "Pending" => data.Where(r => r.Status.Contains("Pending") || financeStatuses.Contains(r.Status)).ToList(),
                    "PendingOnHead" => data.Where(r => r.Status == "Pending_HOS" || r.Status == "Pending_HOO").ToList(),
                    "PaidPendingOnManager" => data.Where(r => managerReviewStatuses.Contains(r.Status)).ToList(),
                    "Denied" => data.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged").ToList(),
                    "Closed" => data.Where(r => r.Status == "Closed").ToList(),
                    _ => data
                };

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Procurement Report");

                    worksheet.Cell(1, 1).Value = "Request ID";
                    worksheet.Cell(1, 2).Value = "Requester";
                    worksheet.Cell(1, 3).Value = "Supplier / Vendor";
                    worksheet.Cell(1, 4).Value = "Description";
                    worksheet.Cell(1, 5).Value = "Cost Type";
                    worksheet.Cell(1, 6).Value = "Amount";
                    worksheet.Cell(1, 7).Value = "Status";
                    worksheet.Cell(1, 8).Value = "Date Created";
                    worksheet.Cell(1, 9).Value = "Quote Type";
                    worksheet.Cell(1, 10).Value = "Customer Name";
                    worksheet.Cell(1, 11).Value = "Department Type";

                    // Format Headers
                    var headerRange = worksheet.Range("A1:H1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");

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
                        var content = stream.ToArray();

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"ProcurementReport_{tab ?? "All"}_{GetSouthAfricanTime():yyyyMMdd}.xlsx");
                    }
                }
            }
            catch
            {
                ModelState.AddModelError("", "An unexpected system routing trace execution fault occurred.");
                return RedirectToPage("/Procurement/Reports");
            }
        }

        public class DepartmentSpendDto
        {
            public string Department { get; set; }
            public decimal TotalSpend { get; set; }
            public int RequestCount { get; set; }
        }

        public class VendorPerformanceDto
        {
            public string Vendor { get; set; }
            public int Requests { get; set; }
            public decimal TotalSpend { get; set; }
            public decimal AvgRequestValue { get; set; }
        }

        public class AuditTrailDto
        {
            public long Id { get; set; }
            public string TableName { get; set; }
            public string RecordId { get; set; }
            public string ActionType { get; set; }
            public string UserName { get; set; }
            public DateTime? Timestamp { get; set; }
        }

        public class BudgetTrendDto
        {
            public string Period { get; set; }
            public decimal TotalSpend { get; set; }
            public int RequestCount { get; set; }
        }

        public class SupplierSpendDto
        {
            public string Supplier { get; set; }
            public decimal TotalSpend { get; set; }
        }

        public class BudgetForecastDto
        {
            public string Period { get; set; }
            public decimal Actual { get; set; }
            public decimal Forecast { get; set; }
        }
    }
}