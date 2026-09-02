using Intranet.Models;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Services
{
    public class PaymentDeadlineWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentDeadlineWorker> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Sweep runtime framework parameters every 4 hours

        public PaymentDeadlineWorker(IServiceProvider serviceProvider, ILogger<PaymentDeadlineWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment Deadline monitoring worker context setup complete.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Identify active requests that have passed their deadline but aren't flagged as overdue yet
                        var itemsBehindSchedule = await context.Requests
                            .Where(r => r.PaymentTiming == "Future Dated"
                                     && r.FutureDate != null
                                     && r.FutureDate < GetSouthAfricanTime()
                                     && !r.IsOverdue
                                     && r.Status != "Completed"
                                     && r.Status != "Rejected")
                            .ToListAsync(stoppingToken);

                        if (itemsBehindSchedule.Any())
                        {
                            _logger.LogWarning("Detected {Count} items past processing milestones. Adjusting index metrics cleanly...", itemsBehindSchedule.Count);

                            foreach (var req in itemsBehindSchedule)
                            {
                                req.IsOverdue = true;
                                req.UpdatedAt = GetSouthAfricanTime();
                            }

                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception thrown attempting execution of procurement background timeline evaluations.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}