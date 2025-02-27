using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;

public class NewsletterController : Controller
{
    [HttpPost]
    public IActionResult Subscribe(string email)
    {
        // Send Email
        try
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("Goplay707@gmail.com", "vkrk xkqu tfey rind"),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("Goplay707@gmail.com"),
                Subject = "Welcome to the Cape Town Festival Newsletter!",
                Body = $"Hi there,\n\nThank you for subscribing to the Cape Town Festival newsletter. Stay tuned for exciting updates!\n\nBest regards,\nThe Cape Town Festival Team",
                IsBodyHtml = false,
            };

            mailMessage.To.Add(email);
            smtpClient.Send(mailMessage);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }

        // Return Success Message
        return Json(new { success = true });
    }
}