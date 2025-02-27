using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Cape_Town_Festival.Models;
using Cape_Town_Festival.Database; 
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Cape_Town_Festival.Controllers
{
  public class HomeController : Controller
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly ILogger<HomeController> _logger;

        public HomeController(FirestoreDb firestoreDb, ILogger<HomeController> logger)
        {
            _firestoreDb = firestoreDb;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

     [HttpPost]
public async Task<IActionResult> SendEmail(string name, string senderEmail, string subject, string message)
{
    try
    {
        var fromAddress = new MailAddress("goplay707@gmail.com", "Cape Town Festival");
        var toAddress = new MailAddress("goplay707@gmail.com"); // Always send to your festival email

        const string fromPassword = "yury qvgu sqwa egbr"; // Your App Password

        string body = $@"
            Name: {name}
            Email: {senderEmail}
            Subject: {subject}

            Message:
            {message}
        ";

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

        using (var mailMessage = new MailMessage(fromAddress, toAddress))
        {
            mailMessage.Subject = $"Contact Us - {subject}";
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = false;

            // Set Reply-To so you can respond to the user
            mailMessage.ReplyToList.Add(new MailAddress(senderEmail));

            await smtp.SendMailAsync(mailMessage);
        }

        TempData["Message"] = "Your message has been sent successfully! We will get back to you soon.";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending email");
        TempData["Error"] = "Failed to send the message. Please try again later.";
    }

    return RedirectToAction("Index");
}
    }       
}
