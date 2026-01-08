using Microsoft.AspNetCore.Identity.UI.Services;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public class EmailSender : IEmailSender
{
    private readonly string smtpUser = "d.minhkhoi070404@gmail.com"; // Gmail của bạn
    private readonly string smtpPass = "lnkz wcws jyia fjcv";         // Mật khẩu ứng dụng

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        using (var client = new SmtpClient("smtp.gmail.com", 587))
        {
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);
            client.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUser, "BookStore Support"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            try
            {
                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"✅ Email đã được gửi tới {email}");
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"❌ SMTP Error: {smtpEx.StatusCode} - {smtpEx.Message}");
                if (smtpEx.InnerException != null)
                    Console.WriteLine($"🔍 Inner: {smtpEx.InnerException.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ General Error khi gửi mail: {ex.Message}");
                throw;
            }
        }
    }
}
