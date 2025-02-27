using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System.Security.Claims;
using System.Threading.Tasks;
using Cape_Town_Festival.Models;
using Cape_Town_Festival.Database; // Import the Database folder for the RSVPDatabase class
using System.Text; // For StringBuilder
using iTextSharp.text; // For PDF
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.qrcode; // For QR Code
using System.IO; // For MemoryStream
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Cape_Town_Festival.Controllers
{
    [Authorize]
    public class RSVPController : Controller
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly ILogger<RSVPController> _logger;
        private readonly IConfiguration _configuration;

   public RSVPController(FirestoreDb firestoreDb, ILogger<RSVPController> logger, IConfiguration configuration)
{
    _firestoreDb = firestoreDb;
    _logger = logger;
    _configuration = configuration;
}

    public async Task<IActionResult> Form(int eventId, string eventName, string category, string location)
    {
        _logger.LogInformation($"Form action called with eventId: {eventId}, eventName: {eventName}");

        try
        {
            // 🛑 Ensure valid event ID
            if (eventId == 0 || string.IsNullOrEmpty(eventName))
            {
                _logger.LogWarning("Invalid RSVP request. Missing event details.");
                TempData["ErrorMessage"] = "Invalid RSVP request. Missing event details.";
                return RedirectToAction("EventsDynamic", "Events");
            }

            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "Unknown Email";

            // 🔹 Fetch User Details from Firestore
            DocumentReference userRef = _firestoreDb.Collection("UserCollection").Document(email);
            DocumentSnapshot userSnapshot = await userRef.GetSnapshotAsync();

            string userFullName = "Guest User";
            if (userSnapshot.Exists)
            {
                var userData = userSnapshot.ToDictionary();
                string firstName = userData.ContainsKey("FirstName") ? userData["FirstName"]?.ToString() ?? "Guest" : "Guest";
                string lastName = userData.ContainsKey("LastName") ? userData["LastName"]?.ToString() ?? "" : "";
                userFullName = $"{firstName} {lastName}";
            }

            // 🔹 Fetch the Correct Event from Firestore
            _logger.LogInformation($"Fetching event details for eventId: {eventId}");
            DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId.ToString());
            DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

            if (!eventSnapshot.Exists)
            {
                _logger.LogWarning($"Event not found: {eventId}");
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToAction("EventsDynamic", "Events");
            }

            var eventData = eventSnapshot.ToDictionary();
            _logger.LogInformation($"Event data retrieved successfully: {eventData["EventName"]}");

            // ✅ Get Maximum Attendees
            int maxAttendees = eventData.ContainsKey("EventMaxAttendees") ? Convert.ToInt32(eventData["EventMaxAttendees"]) : 0;

            // ✅ Fetch RSVPs for this Event **ONLY**
            _logger.LogInformation($"Fetching RSVPs for eventId: {eventId}");
            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection")
                .WhereEqualTo("EventID", eventId); // ✅ Ensure filtering by eventId

            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

            int totalAttendees = 0;
            foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
            {
                if (rsvp.TryGetValue("AttendeeCount", out object attendeeObj) && attendeeObj is long attendeeCount)
                {
                    totalAttendees += (int)attendeeCount;
                }
            }

            // ✅ Correct "Seats Left" Calculation
            int seatsLeft = Math.Max(0, maxAttendees - totalAttendees);
            _logger.LogInformation($"Seats Left for eventId {eventId}: {seatsLeft} (Max: {maxAttendees}, RSVPs: {totalAttendees})");

            // ✅ Get Event Date if Available
            string formattedDate = "Date not set";
            if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp eventTimestamp)
            {
                formattedDate = eventTimestamp.ToDateTime().ToString("dddd, dd MMMM yyyy h:mm tt");
            }

            // ✅ Ensure Data Passed to View is CORRECT
            ViewBag.EventID = eventId;
            ViewBag.EventName = eventName;
            ViewBag.Category = category;
            ViewBag.Location = location ?? "Location Not Provided";
            ViewBag.UserFullName = userFullName;
            ViewBag.UserEmail = email;
            ViewBag.SeatsLeft = seatsLeft;  // ✅ **Ensuring Correct Value**
            ViewBag.StartDate = formattedDate;

            _logger.LogInformation($"Successfully loaded RSVP form for event: {eventName} (Seats Left: {seatsLeft})");
            return View("~/Views/RSVP/RSVP.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading RSVP form: {ex.Message}");
            TempData["ErrorMessage"] = "An error occurred while loading the RSVP form.";
            return RedirectToAction("EventsDynamic", "Events");
        }
    } 

    // Submit RSVP Form
    [HttpPost]
    public async Task<IActionResult> Submit(int eventId, string eventName, string startDate, string category, int attendeeCount, string location)
    {
        try
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var userName = User.Identity.Name ?? "Guest";

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "You must be logged in to RSVP.";
                return RedirectToAction("LogIn", "Account");
            }

            location ??= "Location Not Provided";

            // Get event details from Firestore
            DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId.ToString());
            DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

            if (!eventSnapshot.Exists)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToAction("AccountHome", "Account");
            }

            var eventData = eventSnapshot.ToDictionary();

            // Extract event date
            Timestamp eventTimestamp;
            if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp timestamp)
            {
                eventTimestamp = timestamp;
            }
            else
            {
                TempData["ErrorMessage"] = "Event date not found.";
                return RedirectToAction("Form", new { eventId, eventName, category, location });
            }

            var rsvp = new RSVP
            {
                Email = email,
                EventID = eventId,
                EventName = eventName,
                StartDate = eventTimestamp,
                Category = category,
                AttendeeCount = attendeeCount,
                Location = location
            };

            var rsvpDb = new RSVPDatabase(_firestoreDb);
            var success = await rsvpDb.SaveRSVPAsync(rsvp);

            if (success)
            {
                // Send RSVP Confirmation Email
                SendRSVPConfirmation(email, userName, eventName, startDate, location);

                TempData["SuccessMessage"] = "Your RSVP has been successfully confirmed! A confirmation email has been sent.";
                return RedirectToAction("AccountHome", "Account");
            }
            else
            {
                TempData["ErrorMessage"] = "An error occurred while confirming your RSVP.";
                return RedirectToAction("Form", new { eventId, eventName, category, location });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving RSVP: {ex.Message}");
            TempData["ErrorMessage"] = "An unexpected error occurred.";
            return RedirectToAction("Form", new { eventId, eventName, category, location });
        }
    }

    private void SendRSVPConfirmation(string userEmail, string userName, string eventName, string eventDate, string eventAddress)
{
    try
    {
        var fromAddress = new MailAddress("goplay707@gmail.com", "Cape Town Festival");
        var toAddress = new MailAddress(userEmail);
        const string fromPassword = "yury qvgu sqwa egbr";
        string subject = $"RSVP Confirmation for {eventName}";

        string body = $@"
            Dear {userName},

            Thank you for RSVP'ing for {eventName}!

            📅 Date: {eventDate}
            📍 Location: {eventAddress}

            We look forward to seeing you!

            Regards,  
            Cape Town Festival Team
        ";

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

        using (var message = new MailMessage(fromAddress, toAddress))
        {
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;
            smtp.Send(message);
        }

        Console.WriteLine("✅ Email sent successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Email sending failed: {ex.Message}");
        _logger.LogError($"❌ Email sending failed: {ex.Message}");
    }
}
      
    // Edit RSVP
    [HttpPost]
    public async Task<IActionResult> Edit(int eventId, int newAttendeeCount)
    {
        try
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var userName = User.Identity?.Name ?? "Guest";

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "You must be logged in to edit your RSVP.";
                return RedirectToAction("LogIn", "Account");
            }

            var rsvpDb = new RSVPDatabase(_firestoreDb);
            var success = await rsvpDb.UpdateRSVP(email, eventId, newAttendeeCount);

            TempData["SuccessMessage"] = success ? "Your RSVP has been successfully updated! A Conformation Email has been sent" : "Failed to update your RSVP.";
            SendEditRSVPEmail(email, userName);
            return RedirectToAction("AccountHome", "Account");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating RSVP: {ex.Message}");
            TempData["ErrorMessage"] = "An error occurred while updating your RSVP.";
            return RedirectToAction("AccountHome", "Account");
        }
    }
    private void SendEditRSVPEmail(string userEmail, string userName)
{
    try
    {
        var fromAddress = new MailAddress("goplay707@gmail.com", "Cape Town Festival");
        var toAddress = new MailAddress(userEmail);
        const string fromPassword = "yury qvgu sqwa egbr"; // Your App Password
        string subject = "RSVP Update Confirmation";

        string body = $@"
            Dear {userName},

            This email is to confirm that you have edited an RSVP related to your account.

            Please see your account home for more details.

            Regards,  
            Cape Town Festival Team
        ";

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

        using (var message = new MailMessage(fromAddress, toAddress))
        {
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;
            smtp.Send(message);
        }

        Console.WriteLine("✅ RSVP update email sent successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ RSVP update email failed: {ex.Message}");
    }
}

// Load RSVP Edit Form 
    public async Task<IActionResult> EditForm(string eventId)
    {
        try
        {
            _logger.LogInformation($"EditForm called with eventId: {eventId}");
            
            if (string.IsNullOrEmpty(eventId))
            {
                _logger.LogError("EventId is null or empty");
                TempData["ErrorMessage"] = "Invalid Event ID.";
                return RedirectToAction("AccountHome", "Account");
            }

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            _logger.LogInformation($"User email: {email}");
            
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("User email is null or empty");
                TempData["ErrorMessage"] = "You must be logged in to edit your RSVP.";
                return RedirectToAction("LogIn", "Account");
            }

            // Get current user's RSVP
            var documentId = $"{email}_{eventId}";
            _logger.LogInformation($"Looking for RSVP document with ID: {documentId}");
            
            DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);
            DocumentSnapshot snapshot = await rsvpRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                _logger.LogError($"RSVP not found for document ID: {documentId}");
                TempData["ErrorMessage"] = "RSVP not found.";
                return RedirectToAction("AccountHome", "Account");
            }

            var rsvpData = snapshot.ToDictionary();
            
            // Get current attendee count
            int currentAttendeeCount = Convert.ToInt32(rsvpData.GetValueOrDefault("AttendeeCount", 1));
            _logger.LogInformation($"Current user's attendee count: {currentAttendeeCount}");

            // Fetch event details
            DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
            DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

            if (!eventSnapshot.Exists)
            {
                _logger.LogError($"Event not found for ID: {eventId}");
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToAction("AccountHome", "Account");
            }

            var eventData = eventSnapshot.ToDictionary();

            // ✅ Get Event Start Date (Same as RSVP Form)
            string formattedStartDate = "Date not set";
            if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp eventTimestamp)
            {
                formattedStartDate = eventTimestamp.ToDateTime().ToString("dddd, dd MMMM yyyy h:mm tt");
            }

            // Calculate total attendees (excluding current user)
            int maxAttendees = eventData.ContainsKey("EventMaxAttendees") ? Convert.ToInt32(eventData["EventMaxAttendees"]) : 0;
            _logger.LogInformation($"Event max attendees: {maxAttendees}");

            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", eventId);
            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

            int totalAttendees = 0;
            foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
            {
                string rsvpEmail = rsvp.GetValue<string>("Email");
                if (rsvpEmail != email) // Exclude current user's count
                {
                    if (rsvp.TryGetValue("AttendeeCount", out object attendeeObj) && attendeeObj is long attendeeCount)
                    {
                        totalAttendees += (int)attendeeCount;
                    }
                }
            }

            _logger.LogInformation($"Total attendees (excluding current user): {totalAttendees}");
            
            // Calculate available seats (including current user's allocation)
            int seatsLeft = Math.Max(0, maxAttendees - totalAttendees);
            _logger.LogInformation($"Available seats: {seatsLeft}");

            // Set ViewBag data
            ViewBag.EventID = eventId;
            ViewBag.EventName = rsvpData.GetValueOrDefault("EventName", "Unknown Event")?.ToString();
            ViewBag.StartDate = formattedStartDate; 
            ViewBag.Category = rsvpData.GetValueOrDefault("Category", "")?.ToString();
            ViewBag.Location = rsvpData.GetValueOrDefault("Location", "")?.ToString();
            ViewBag.UserFullName = User.FindFirst(ClaimTypes.Name)?.Value ?? email;
            ViewBag.UserEmail = email;
            ViewBag.AttendeeCount = currentAttendeeCount;
            ViewBag.SeatsLeft = seatsLeft;

            _logger.LogInformation("Successfully prepared edit form");
            return View("~/Views/RSVP/RSVPEdit.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading RSVP edit form: {ex.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            TempData["ErrorMessage"] = "An error occurred while loading the edit form.";
            return RedirectToAction("AccountHome", "Account");
        }
    }

    //Cancel RSVP by Visitor
    [HttpPost]
        public async Task<IActionResult> Cancel(int eventId)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var userName = User.Identity?.Name ?? "Guest";

                if (string.IsNullOrEmpty(email))
                {
                    TempData["ErrorMessage"] = "You must be logged in to cancel your RSVP.";
                    return RedirectToAction("LogIn", "Account");
                }

                var rsvpDb = new RSVPDatabase(_firestoreDb);
                var success = await rsvpDb.CancelRSVPAsync(email, eventId.ToString());

                if (success)
                {
                    SendCancelRSVPEmail(email, userName);
                    TempData["SuccessMessage"] = "Your RSVP has been successfully canceled. A cancellation EMail has been sent to your email address too.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to cancel RSVP. Please try again.";
                }

                return RedirectToAction("AccountHome", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error canceling RSVP: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while canceling your RSVP.";
                return RedirectToAction("AccountHome", "Account");
            }
        }

        private void SendCancelRSVPEmail(string userEmail, string userName)
{
    try
    {
        var fromAddress = new MailAddress("goplay707@gmail.com", "Cape Town Festival");
        var toAddress = new MailAddress(userEmail);
        const string fromPassword = "yury qvgu sqwa egbr"; // Your App Password
        string subject = "RSVP Cancellation Confirmation";

        string body = $@"
            Dear {userName},

            This email is to confirm that you have canceled an RSVP related to your account.

            If this was a mistake, you can RSVP again at any time.

            Regards,  
            Cape Town Festival Team
        ";

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

        using (var message = new MailMessage(fromAddress, toAddress))
        {
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;
            smtp.Send(message);
        }

        Console.WriteLine(" RSVP cancellation email sent successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($" RSVP cancellation email failed: {ex.Message}");
    }
}

    // Handle "Event Too Far in Advance"
    public IActionResult EventToFar(string eventName, DateTime eventDate)
    {
        if (string.IsNullOrEmpty(eventName) || eventDate == default)
        {
            return View("Error"); // If data is missing, show an error page.
        }

        var reminderDate = eventDate.AddMonths(-6);

        var viewModel = new EventViewModel
        {
            EventName = eventName,
            EventDate = Google.Cloud.Firestore.Timestamp.FromDateTime(eventDate), // Fixed this line
        };

        return View("EventToFar", viewModel);
    }


        public async Task<IActionResult> GenerateCalendarEvent(string eventId)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return RedirectToAction("LogIn", "Account");

                // Fetch event details from EventsCollection
                DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
                DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

                if (!eventSnapshot.Exists)
                    return NotFound();

                var eventData = eventSnapshot.ToDictionary();
                
                // Get event details directly from EventsCollection
                var eventName = eventData["EventName"].ToString();
                var location = eventData.GetValueOrDefault("EventLocation", "Location TBA").ToString();
                
                // Get location coordinates if available
                if (eventData.ContainsKey("EventLatLong") && eventData["EventLatLong"] is GeoPoint geoPoint)
                {
                    location = $"{location} ({geoPoint.Latitude}, {geoPoint.Longitude})";
                }

                // Get the correct date from EventsCollection with UTC+2
                DateTime startDate;
                if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp eventTimestamp)
                {
                    startDate = eventTimestamp.ToDateTime().ToLocalTime();
                }
                else
                {
                    TempData["ErrorMessage"] = "Event date not found.";
                    return RedirectToAction("AccountHome", "Account");
                }

                var endDate = startDate.AddHours(4); // Default 4-hour duration

                // Generate .ics file
                var calendar = new StringBuilder();
                calendar.AppendLine("BEGIN:VCALENDAR");
                calendar.AppendLine("VERSION:2.0");
                calendar.AppendLine("BEGIN:VEVENT");
                calendar.AppendLine($"DTSTART:{startDate:yyyyMMddTHHmmss}");
                calendar.AppendLine($"DTEND:{endDate:yyyyMMddTHHmmss}");
                calendar.AppendLine($"SUMMARY:{eventName}");
                calendar.AppendLine($"LOCATION:{location}");
                calendar.AppendLine("END:VEVENT");
                calendar.AppendLine("END:VCALENDAR");

                var calendarBytes = Encoding.UTF8.GetBytes(calendar.ToString());
                
                // Set headers to open in new tab instead of downloading
                Response.Headers.Add("Content-Disposition", "inline; filename=" + $"{eventName}.ics");
                return File(calendarBytes, "text/calendar");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating calendar event: {ex.Message}");
                TempData["ErrorMessage"] = "Unable to generate calendar event.";
                return RedirectToAction("AccountHome", "Account");
            }
        }
        public async Task<IActionResult> DisplayETicket(string eventId)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return RedirectToAction("LogIn", "Account");

                // Fetch User Details (Full Name)
                DocumentReference userRef = _firestoreDb.Collection("UserCollection").Document(email);
                DocumentSnapshot userSnapshot = await userRef.GetSnapshotAsync();
                
                string fullName = "Guest User";
                string firstName = "Guest";
                if (userSnapshot.Exists)
                {
                    var userData = userSnapshot.ToDictionary();
                    firstName = userData.ContainsKey("FirstName") ? userData["FirstName"].ToString() : "Guest";
                    string lastName = userData.ContainsKey("LastName") ? userData["LastName"].ToString() : "";
                    fullName = $"{firstName} {lastName}";
                }

                // Get event details from EventsCollection
                DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
                DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

                if (!eventSnapshot.Exists)
                    return NotFound();

                var eventData = eventSnapshot.ToDictionary();
                
                // Get event details
                string formattedStartDate;
                if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp eventTimestamp)
                {
                    var localDateTime = eventTimestamp.ToDateTime().ToLocalTime();
                    formattedStartDate = localDateTime.ToString("dddd, dd MMMM yyyy h:mm tt");
                }
                else
                {
                    formattedStartDate = "Date not available";
                }

                // Get location coordinates
                string latitude = "0", longitude = "0";
                if (eventData.ContainsKey("EventLatLong") && eventData["EventLatLong"] is GeoPoint geoPoint)
                {
                    latitude = geoPoint.Latitude.ToString();
                    longitude = geoPoint.Longitude.ToString();
                }

                // Fetch RSVP Details
                var documentId = $"{email}_{eventId}";
                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);
                DocumentSnapshot rsvpSnapshot = await rsvpRef.GetSnapshotAsync();

                if (!rsvpSnapshot.Exists)
                    return NotFound();

                var rsvpData = rsvpSnapshot.ToDictionary();
                var eventName = eventData["EventName"]?.ToString() ?? "Event";
                var location = eventData["EventLocation"]?.ToString() ?? "Location not specified";
                var attendeeCount = rsvpData["AttendeeCount"]?.ToString() ?? "0";
                var downloadTime = DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");

                // Generate Static Map URL
                var googleMapsApiKey = _configuration["GoogleMaps:ApiKey"] ?? "YOUR_API_KEY";
                string mapUrl = $"https://maps.googleapis.com/maps/api/staticmap?" +
                            $"center={latitude},{longitude}&" +
                            "zoom=15&" +
                            "size=400x200&" +
                            $"markers=color:red|label:{eventName}|{latitude},{longitude}&" +
                            $"key={googleMapsApiKey}";

                using (MemoryStream ms = new MemoryStream())
                {
                    using (Document document = new Document(PageSize.A5, 36f, 36f, 54f, 36f))
                    {
                        PdfWriter writer = PdfWriter.GetInstance(document, ms);
                        document.Open();

                        // Set up fonts
                        var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        var welcomeFont = new Font(baseFont, 30, Font.BOLD, new BaseColor(0, 0, 139));
                        var titleFont = new Font(baseFont, 22, Font.BOLD, BaseColor.BLACK);
                        var normalFont = new Font(baseFont, 12, Font.NORMAL);
                        var italicFont = new Font(baseFont, 12, Font.ITALIC, BaseColor.MAGENTA);
                        var smallFont = new Font(baseFont, 10, Font.NORMAL, BaseColor.DARK_GRAY);

                        // Welcome Message
                        var welcomeText = new Paragraph($"Welcome, {firstName}!", welcomeFont);
                        welcomeText.Alignment = Element.ALIGN_CENTER;
                        welcomeText.SpacingAfter = 3f;
                        document.Add(welcomeText);

                        var eventText = new Paragraph($"to {eventName}", titleFont);
                        eventText.Alignment = Element.ALIGN_CENTER;
                        eventText.SpacingAfter = 10f;
                        document.Add(eventText);

                        // Event Details
                        var emailText = new Paragraph($"Email: {email}", normalFont);
                        emailText.SpacingAfter = 5f;
                        document.Add(emailText);

                        var dateDetails = new Paragraph($"Date: {formattedStartDate}", normalFont);
                        dateDetails.SpacingAfter = 5f;
                        document.Add(dateDetails);

                        var locationDetails = new Paragraph($"Location: {location}", normalFont);
                        locationDetails.SpacingAfter = 5f;
                        document.Add(locationDetails);

                        var attendeeDetails = new Paragraph($"Number of Attendees: {attendeeCount}", normalFont);
                        attendeeDetails.SpacingAfter = 10f;
                        document.Add(attendeeDetails);

                        // Fun Message
                        var funMessage = new Paragraph("Enjoy the event & use #CapeTownFestivals!", italicFont);
                        funMessage.Alignment = Element.ALIGN_CENTER;
                        funMessage.SpacingAfter = 10f;
                        document.Add(funMessage);

                        // Add QR Code
                        var qrContent = $"Event: {eventName}\nDate: {formattedStartDate}\nAttendee: {fullName}";
                        var qrCode = new BarcodeQRCode(qrContent, 100, 100, null);
                        var qrImage = qrCode.GetImage();
                        qrImage.Alignment = Element.ALIGN_CENTER;
                        qrImage.ScaleToFit(120f, 120f);
                        document.Add(qrImage);

                        // Add Map at the bottom
                        try
                        {
                            using (var client = new HttpClient())
                            {
                                var imageBytes = await client.GetByteArrayAsync(mapUrl);
                                var mapImage = Image.GetInstance(imageBytes);
                                mapImage.ScaleToFit(250f, 125f);
                                mapImage.Alignment = Element.ALIGN_CENTER;
                                mapImage.SpacingBefore = 10f;
                                mapImage.SpacingAfter = 5f;
                                document.Add(mapImage);

                                var mapNote = new Paragraph("Event Location Map", smallFont);
                                mapNote.Alignment = Element.ALIGN_CENTER;
                                mapNote.SpacingAfter = 10f;
                                document.Add(mapNote);
                            }
                        }
                        catch (Exception mapEx)
                        {
                            _logger.LogError($"Error adding map to ticket: {mapEx.Message}");
                        }

                        // Downloaded Timestamp
                        var timestampText = new Paragraph($"Downloaded at: {downloadTime}", smallFont);
                        timestampText.Alignment = Element.ALIGN_CENTER;
                        timestampText.SpacingAfter = 10f;
                        document.Add(timestampText);

                        // Support Contact
                        var supportText = new Paragraph("Need assistance? Contact our support team:", normalFont);
                        supportText.Alignment = Element.ALIGN_CENTER;
                        document.Add(supportText);

                        var supportEmail = new Anchor("Support@CTFestival.com", normalFont);
                        supportEmail.Reference = "mailto:goplay707@gmail.com";
                        var supportParagraph = new Paragraph();
                        supportParagraph.Alignment = Element.ALIGN_CENTER;
                        supportParagraph.Add(supportEmail);
                        document.Add(supportParagraph);

                        document.Close();
                    }

                    // Set response headers to open in new tab
                    Response.Headers.Add("Content-Disposition", "inline; filename=" + $"e-Ticket_{eventName}.pdf");
                    return File(ms.ToArray(), "application/pdf");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating e-Ticket: {ex.Message}");
                TempData["ErrorMessage"] = "Unable to generate e-Ticket.";
                return RedirectToAction("AccountHome", "Account");
            }
        }
    }
}