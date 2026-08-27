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
                .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == targetRole))
                .ToListAsync();
            foreach (var user in usersInRole)
            {
                await AddToDatabaseAndEmail(user.Id, user.Email, requestId, "Action Required", message, "RoleAlert");
            }
            await _context.SaveChangesAsync();
        }

        public async Task NotifyUserAsync(Guid userId, string message, int? requestId = null, string title = "Status Update")
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                await AddToDatabaseAndEmail(userId, user.Email, requestId, title, message, "DirectUpdate");
                await _context.SaveChangesAsync();
            }
        }

        private async Task AddToDatabaseAndEmail(Guid userId, string email, int? requestId, string title, string message, string type)
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
            await SendEmailAsync(email, $"Procurement: {title}", message);
        }

       
        private async Task SendEmailAsync(string email, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _fromEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = body,
                    HtmlBody = $"<p>{body}</p>"
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_fromEmail, _appPassword);
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
                throw; 
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
                    TextBody = "Please find your personalized monthly report attached.",
                    HtmlBody = "<p>Please find your personalized monthly report attached.</p>"
                };

                bodyBuilder.Attachments.Add(fileName, excelBytes, ContentType.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_fromEmail, _appPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google SMTP Report Error: {ex.Message}");
                throw;
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
                    TextBody = "Attached are your personalized report and the master procurement register.",
                    HtmlBody = "<h3>Monthly Close</h3><p>Please find the report and the master paid-requests register attached.</p>"
                };

                var contentType = ContentType.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                bodyBuilder.Attachments.Add(reportName, reportBytes, contentType);
                bodyBuilder.Attachments.Add(registerName, registerBytes, contentType);
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_fromEmail, _appPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google SMTP Dual Error: {ex.Message}");
                throw;
            }
        }
    }
}