using Intranet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Intranet.Services
{
    public class MonthlyPaymentWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MonthlyPaymentWorker> _logger;

        // Check once every 12 hours (keeps overhead low while guaranteeing execution on the 1st)
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);
        private int? _lastExecutedMonth = null;

        public MonthlyPaymentWorker(IServiceScopeFactory scopeFactory, ILogger<MonthlyPaymentWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Automated Monthly Payment Worker Service is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // Condition: It must be the 1st day of the month, and we shouldn't have executed yet this month
                if (now.Day == 1 && _lastExecutedMonth != now.Month)
                {
                    try
                    {
                        await ProcessMonthlyPaymentsAsync();
                        _lastExecutedMonth = now.Month; // Mark this month as completed
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred during the automated master monthly payment run.");
                    }
                }

                // Wait 12 hours before checking the calendar date again
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessMonthlyPaymentsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _logger.LogInformation("Beginning monthly payment reconciliation routine...");

            // Fetch all requests that are sitting in the final payment queue waiting for the monthly pay date
            var pendingPayments = await context.Requests
                .Where(r => r.Status == "PO_Payment_Queue")
                .ToListAsync();

            if (!pendingPayments.Any())
            {
                _logger.LogInformation("No recurring PO items found in 'PO_Payment_Queue' to close.");
                return;
            }

            int closedCount = 0;

            
            Guid systemJobUserGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

            foreach (var req in pendingPayments)
            {
                req.Status = "Closed";
                req.UpdatedAt = DateTime.Now;
                closedCount++;

                // Inject a system trace into your transaction audit trail for tracking
                var auditLog = new AuditLog
                {
                    TableName = "Requests",
                    RecordId = req.Id.ToString(),
                    ActionBy = systemJobUserGuid,
                    ActionType = "AUTOMATED_CLOSE",
                    NewValues = $"Status transitioned from 'PO_Payment_Queue' to 'Closed' via automated monthly business payment run."
                    
                };

                
                context.AuditLogs.Add(auditLog);
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Master monthly payment run completed successfully. {Count} requests moved to Archive/Closed.", closedCount);
        }
    }
}