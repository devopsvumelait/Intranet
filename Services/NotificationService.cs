using Intranet.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Intranet.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _host;
        private readonly int _port;
        private readonly string _senderName;
        private readonly string _fromEmail;
        private readonly string _appPassword;
        private readonly string _username;

        private const string LoginUrl = "https://vumela-procurement-bufucdfhcnadfmdz.southafricanorth-01.azurewebsites.net/Account/Login";

        public NotificationService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;

            _host = _config["SmtpSettings:Host"] ?? "smtp.office365.com";
            _port = int.Parse(_config["SmtpSettings:Port"] ?? "587");
            _senderName = _config["SmtpSettings:SenderName"] ?? "Vumela Procurement";
            _fromEmail = _config["SmtpSettings:FromEmail"] ?? "noreplyprocurement@vumelait.co.za";
            _username = _config["SmtpSettings:Username"] ?? "devops@vumelait.co.za";

            // Pulls from Key Vault seamlessly using standard colon or double-dash fallbacks, removing any stray spaces
            var rawPassword = _config["SmtpSettings:AppPassword"] ?? _config["SmtpSettings--AppPassword"] ?? "";
            _appPassword = rawPassword.Replace(" ", "");
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            foreach (var n in notifications) n.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task NotifyApproversAsync(int requestId, string targetRole, string message)
        {
            var usersInRole = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == targetRole))
                .ToListAsync();
            var request = await _context.Requests
                .Include(r => r.Quotes)
                .Include(r => r.Requester)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            string supplierName = "N/A";
            string requestType = request?.RequestType ?? "Standard";
            string quoteType = request?.QuoteType ?? "N/A";
            DateTime dateCreated = request?.CreatedAt ?? DateTime.UtcNow;

            string createdByName = "N/A";
            if (request?.Requester != null)
            {
                createdByName = $"{request.Requester.FirstName} {request.Requester.Surname}".Trim();
                if (string.IsNullOrEmpty(createdByName)) createdByName = request.Requester.Email ?? "N/A";
            }

            var winningQuote = request?.Quotes?.FirstOrDefault(q => q.IsSelected);
            if (winningQuote != null && !string.IsNullOrEmpty(winningQuote.SupplierName))
            {
                supplierName = winningQuote.SupplierName;
            }

            foreach (var user in usersInRole)
            {
                string recipientName = !string.IsNullOrEmpty(user.FirstName) ? user.FirstName : "Valued User";
                if (string.IsNullOrEmpty(recipientName)) recipientName = "Valued User";

                await AddToDatabaseAndEmail(
                    user.Id,
                    user.Email,
                    requestId,
                    "Action Required",
                    message,
                    "RoleAlert",
                    recipientName,
                    supplierName,
                    requestType,
                    quoteType,
                    dateCreated,
                    createdByName
                );
            }
            await _context.SaveChangesAsync();
        }

        public async Task NotifyUserAsync(Guid userId, string message, int? requestId = null, string title = "Status Update")
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                string recipientName = !string.IsNullOrEmpty(user.FirstName) ? user.FirstName : "Valued User";
                if (string.IsNullOrEmpty(recipientName)) recipientName = "Valued User";

                string supplierName = "N/A";
                string requestType = "Standard";
                string quoteType = "N/A";
                DateTime dateCreated = DateTime.UtcNow;
                string createdByName = "N/A";

                if (requestId.HasValue)
                {
                    var request = await _context.Requests
                        .Include(r => r.Quotes)
                        .Include(r => r.Requester)
                        .FirstOrDefaultAsync(r => r.Id == requestId.Value);

                    if (request != null)
                    {
                        requestType = request.RequestType ?? "Standard";
                        quoteType = request.QuoteType ?? "N/A";
                        dateCreated = request.CreatedAt ?? DateTime.UtcNow;

                        if (request.Requester != null)
                        {
                            createdByName = $"{request.Requester.FirstName} {request.Requester.Surname}".Trim();
                            if (string.IsNullOrEmpty(createdByName)) createdByName = request.Requester.Email ?? "N/A";
                        }

                        var winningQuote = request.Quotes?.FirstOrDefault(q => q.IsSelected);
                        if (winningQuote != null && !string.IsNullOrEmpty(winningQuote.SupplierName))
                        {
                            supplierName = winningQuote.SupplierName;
                        }
                    }
                }

                await AddToDatabaseAndEmail(
                    userId,
                    user.Email,
                    requestId,
                    title,
                    message,
                    "DirectUpdate",
                    recipientName,
                    supplierName,
                    requestType,
                    quoteType,
                    dateCreated,
                    createdByName
                );
                await _context.SaveChangesAsync();
            }
        }

        private async Task AddToDatabaseAndEmail(
            Guid userId,
            string email,
            int? requestId,
            string title,
            string message,
            string type,
            string recipientName = "Valued User",
            string supplierName = "N/A",
            string requestType = "Standard",
            string quoteType = "N/A",
            DateTime? dateCreated = null,
            string createdByName = "N/A")
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                RequestId = requestId,
                Title = title,
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            await SendEmailAsync(email, $"Procurement: {title}", message, requestId, recipientName, supplierName, requestType, quoteType, dateCreated, createdByName);
        }


        private async Task SendEmailAsync(
            string email,
            string subject,
            string body,
            int? requestId = null,
            string recipientName = "Valued User",
            string supplierName = "N/A",
            string requestType = "Standard",
            string quoteType = "N/A",
            DateTime? dateCreated = null,
            string createdByName = "N/A")
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _fromEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = subject;

                string displayRequestId = requestId.HasValue ? $"#{requestId.Value}" : "N/A";
                string formattedDate = dateCreated.HasValue ? dateCreated.Value.ToString("yyyy-MM-dd HH:mm") : DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                // Rich HTML Email format tailored to the user and procurement details
                string htmlBody = $@"
                    <div style='font-family: Arial, sans-serif; color: #333; line-height: 1.6; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                        <h2 style='color: #004080; margin-top: 0;'>Vumela Procurement Notification</h2>
                        <p>Hello <strong>{recipientName}</strong>,</p>
                        <p style='background-color: #f9f9f9; padding: 12px; border-left: 4px solid #004080; border-radius: 4px;'>{body}</p>
                        
                        {(requestId.HasValue ? $@"
                        <table style='width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 14px;'>
                            <tr style='background-color: #f2f2f2;'><th colspan='2' style='padding: 8px; text-align: left; border: 1px solid #ddd;'>Request Details</th></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd; width: 35%;'><strong>Request ID:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{displayRequestId}</td></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd;'><strong>Created By:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{createdByName}</td></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd;'><strong>Supplier:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{supplierName}</td></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd;'><strong>Request Type:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{requestType}</td></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd;'><strong>Quote Type:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{quoteType}</td></tr>
                            <tr><td style='padding: 8px; border: 1px solid #ddd;'><strong>Created At:</strong></td><td style='padding: 8px; border: 1px solid #ddd;'>{formattedDate}</td></tr>
                        </table>" : "")}

                        <p style='margin-top: 20px;'>
                            <a href='{LoginUrl}' style='background-color: #004080; color: white; padding: 10px 18px; text-decoration: none; border-radius: 5px; display: inline-block;'>Login to Vumela Procurement</a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='font-size: 12px; color: #777;'>This is an automated system message from Vumela Procurement. Please do not reply directly to this email.</p>
                    </div>";

                string plainTextBody = $"Hello {recipientName},\n\n{body}\n\nRequest ID: {displayRequestId}\nSupplier: {supplierName}\nRequest Type: {requestType}\nQuote Type: {quoteType}\n\nLogin to Vumela Procurement: {LoginUrl}";

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = plainTextBody,
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_username, _appPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"========================================");
                Console.WriteLine($"GOOGLE SMTP ERROR: {ex.Message}");
                Console.WriteLine($"STACK TRACE: {ex.StackTrace}");
                Console.WriteLine($"========================================");
            }
        }


        public async Task SendReportEmailAsync(string email, byte[] excelBytes, string fileName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _fromEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = "Monthly Procurement Report";

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = $"Please find your personalized monthly report attached.\n\nLogin to Vumela Procurement: {LoginUrl}",
                    HtmlBody = $"<p>Please find your personalized monthly report attached.</p><p>Login to Vumela Procurement: <a href='{LoginUrl}'>{LoginUrl}</a></p>"
                };

                bodyBuilder.Attachments.Add(fileName, excelBytes, ContentType.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_username, _appPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google SMTP Report Error: {ex.Message}");
            }
        }


        public async Task SendDualAttachmentEmailAsync(string email, byte[] reportBytes, string reportName, byte[] registerBytes, string registerName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _fromEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = "Monthly Procurement Close: Report & Register";

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = $"Attached are your personalized report and the master procurement register.\n\nLogin to Vumela Procurement: {LoginUrl}",
                    HtmlBody = $"<p>Please find the report and the master paid-requests register attached.</p><p>Login to Vumela Procurement: <a href='{LoginUrl}'>{LoginUrl}</a></p>"
                };

                var contentType = ContentType.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                bodyBuilder.Attachments.Add(reportName, reportBytes, contentType);
                bodyBuilder.Attachments.Add(registerName, registerBytes, contentType);
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_username, _appPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google SMTP Dual Error: {ex.Message}");
            }
        }
    }
}