using Intranet.Models;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Services
{
    public class ReportEmailWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _testRunCompleted = false;

        public ReportEmailWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = GetSouthAfricanTime();

                if (!_testRunCompleted)
                {
                    await ProcessReports(stoppingToken);
                    _testRunCompleted = true;
                }

                if (now.Day == 1 && now.Hour == 7) //Auto reports for pre month
                {
                    await ProcessReports(stoppingToken);
                    await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task ProcessReports(CancellationToken stoppingToken)
        {
            Console.WriteLine($"[WORKER] Automation started at: {GetSouthAfricanTime()}");
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var reportService = scope.ServiceProvider.GetRequiredService<ProcurementReportService>();
                var emailService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                //var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var blobService = scope.ServiceProvider.GetRequiredService<IAzureBlobService>();

                var users = await context.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Where(u => u.IsActive).ToListAsync();
                Console.WriteLine($"[WORKER] Found {users.Count} active users to process.");

                string registerFileName = $"Procurement_Register_{GetSouthAfricanTime():MMMM_yyyy}.xlsx";
               // string registerPath = Path.Combine(webHostEnvironment.WebRootPath, "registers", registerFileName);
                byte[]? registerBytes = null;

                try
                {
                    using var registerStream = await blobService.DownloadFileAsync("registers", registerFileName);
                    if (registerStream != null)
                    {
                        using var ms = new MemoryStream();
                        await registerStream.CopyToAsync(ms);
                        registerBytes = ms.ToArray();
                        Console.WriteLine("[WORKER] Master Register found and loaded from Blob Storage.");
                    }
                    else
                    {
                        Console.WriteLine($"[WORKER] WARNING: Master Register not found in 'registers' container.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WORKER] WARNING: Could not load Master Register: {ex.Message}");
                }

                foreach (var user in users)
                {
                    Console.WriteLine($"[WORKER] Processing User: {user.Email}");

                    // 1. Check if already exists
                    bool alreadyExists = await context.Documents.AnyAsync(d =>
                        d.UploadedById == user.Id &&
                        d.DocType == "Monthly_Report" &&
                        d.UploadedAt.Value.Month == GetSouthAfricanTime().Month &&
                        d.UploadedAt.Value.Year == GetSouthAfricanTime().Year);

                    if (alreadyExists)
                    {
                        Console.WriteLine($"[WORKER] Skipping {user.Email}: Report already exists for this month.");
                        continue;
                    }

                    // 2. Get Data
                    var userReportData = await reportService.GetReportDataForUser(user);
                    if (userReportData.Count == 0)
                    {
                        Console.WriteLine($"[WORKER] Skipping {user.Email}: No procurement data found.");
                        continue;
                    }

                    // 3. Generate and Archive
                    var reportBytes = reportService.GenerateExcel(userReportData);
                    var reportFileName = $"MonthlyReport_{user.Surname}_{GetSouthAfricanTime():yyyyMMdd_HHmm}.xlsx";
                    //await SaveAndArchive(context, webHostEnvironment, user, reportBytes, reportFileName);
                    string blobUrl;
                    using (var reportStream = new MemoryStream(reportBytes))
                    {
                        blobUrl = await blobService.UploadFileAsync("reports", reportFileName, reportStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    }

                    context.Documents.Add(new Document
                    {
                        FileName = reportFileName,
                        BlobUrl = blobUrl, 
                        DocType = "Monthly_Report",
                        RequestId = null,
                        UploadedAt = GetSouthAfricanTime(),
                        UploadedById = user.Id
                    });
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[WORKER] Report archived to Blob Storage for {user.Surname}.");

                    // 4. Email
                    bool isFinance = user.UserRoles.Any(r => r.Role.RoleName == "Finance");
                    bool isMD = user.UserRoles.Any(r => r.Role.RoleName == "MD");
                    bool isHOO = user.UserRoles.Any(r => r.Role.RoleName == "HOO");
                    if ((isFinance || isMD || isHOO) && registerBytes != null)
                    {
                        await emailService.SendDualAttachmentEmailAsync(user.Email, reportBytes, reportFileName, registerBytes, registerFileName);
                        Console.WriteLine($"[WORKER] Dual Email sent to Finance: {user.Email}");
                    }
                    else
                    {
                        await emailService.SendReportEmailAsync(user.Email, reportBytes, reportFileName);
                        Console.WriteLine($"[WORKER] Standard Email sent to: {user.Email}");
                    }
                }
                Console.WriteLine("[WORKER] Automation completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WORKER] CRITICAL ERROR: {ex.Message}");
            }
        }

       /* private async Task SaveAndArchive(AppDbContext context, IWebHostEnvironment env, User user, byte[] bytes, string fileName)
        {
            var uploadsPath = Path.Combine(env.WebRootPath, "Uploads", "Reports");
            if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
            await File.WriteAllBytesAsync(Path.Combine(uploadsPath, fileName), bytes);

            context.Documents.Add(new Document
            {
                FileName = fileName,
                BlobUrl = $"/Uploads/Reports/{fileName}",
                DocType = "Monthly_Report",
                RequestId = null,
                UploadedAt = GetSouthAfricanTime(),
                UploadedById = user.Id
            });
            await context.SaveChangesAsync();
        } */
    }
}