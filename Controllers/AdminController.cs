using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Cape_Town_Festival.Database; 
using Microsoft.AspNetCore.Authorization;
using Cape_Town_Festival.Models;

namespace Cape_Town_Festival.Controllers
{
    [Authorize(Roles = "Admin")] // Restrict access to Admins only
    public class AdminController : Controller
{
    private readonly FirestoreDb _firestoreDb;
    private readonly AdminDatabase _adminDb;
    private readonly EventsDatabase _eventsDb;
    private readonly ILogger<AdminController> _logger;

      public AdminController(FirestoreDb firestoreDb, ILogger<AdminController> logger, ILogger<EventsDatabase> eventLogger)
    {
        _firestoreDb = firestoreDb;
        _adminDb = new AdminDatabase(firestoreDb);
        _eventsDb = new EventsDatabase(firestoreDb, eventLogger); //  Pass eventLogger here
        _logger = logger;
    }

        //  GET: Admin Dashboard with Event Listings
        [HttpGet]
        public async Task<IActionResult> AdminHome()
        {
            if (!User.IsInRole("Admin"))
            {
                _logger.LogWarning("Unauthorized admin access attempt.");
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Login");
            }

            var events = await _eventsDb.GetAllEventsAsync();
            return View("AdminHome", events);
        }

        //  GET: Admin Login Page
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View("AdminLogin");
        }

        //  POST: Admin Login Authentication
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                bool isValidAdmin = await _adminDb.AuthenticateAdminAsync(email, password);

                if (!isValidAdmin)
                {
                    _logger.LogWarning($"Failed login attempt for admin: {email}");
                    TempData["ErrorMessage"] = "Invalid admin credentials.";
                    return RedirectToAction("Login");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

                _logger.LogInformation($" Admin {email} logged in successfully.");
                return RedirectToAction("AdminHome");
            }
            catch (Exception ex)
            {
                _logger.LogError($" Admin login error: {ex.Message}");
                TempData["ErrorMessage"] = "Error during login. Please try again.";
                return RedirectToAction("Login");
            }
        }


        //  GET: Logout Admin
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation(" Admin logged out.");
            return RedirectToAction("Login");
        }

       // GET: Load the Create Event form
[HttpGet]
public IActionResult CreateEvent()
{
    return View("CreateEvent");
}

// POST: Handle the form submission
[HttpPost]
public async Task<IActionResult> CreateEventPost(EventViewModel newEvent)
{
    try
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid form data" });
        }

        //  Debugging: Log all received form values
        foreach (var key in Request.Form.Keys)
        {
            _logger.LogInformation($"📌 Received Form Key: {key}, Value: {Request.Form[key]}");
        }

        //  Fix: Get Event Date from form input with debugging
        string eventDateString = Request.Form["EventDate"];

        //  Handle multiple values issue
        if (!string.IsNullOrWhiteSpace(eventDateString) && eventDateString.Contains(","))
        {
            _logger.LogWarning($"⚠️ Multiple EventDate values detected: {eventDateString}");
            eventDateString = eventDateString.Split(',')[0];  // Take the first value
        }

        //  Log the received value after fix
        _logger.LogInformation($"📌 Final Processed EventDate: {eventDateString}");

        if (string.IsNullOrWhiteSpace(eventDateString))
        {
            _logger.LogError(" Error: EventDate is null or empty.");
            return Json(new { success = false, message = "Event Date cannot be empty." });
        }

        //  Try multiple parsing formats
        DateTime selectedEventDate;
        if (!DateTime.TryParse(eventDateString, out selectedEventDate))
        {
            // Try ISO format (used by JavaScript .toISOString())
            if (!DateTime.TryParseExact(eventDateString, "yyyy-MM-ddTHH:mm:ss.fffZ",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out selectedEventDate))
            {
                _logger.LogError($" Error: Failed to parse EventDate: {eventDateString}");
                return Json(new { success = false, message = "Invalid Event Date format. Please use a valid date." });
            }
        }

        // Convert to UTC
        selectedEventDate = selectedEventDate.ToUniversalTime();
        _logger.LogInformation($" Parsed EventDate: {selectedEventDate}");

        //  Fix: Get Latitude & Longitude safely
        string latitudeString = Request.Form["latitude"];
        string longitudeString = Request.Form["longitude"];

        if (string.IsNullOrWhiteSpace(latitudeString) || string.IsNullOrWhiteSpace(longitudeString))
        {
            _logger.LogError(" Error: Latitude or Longitude is missing.");
            return Json(new { success = false, message = "Latitude and Longitude cannot be empty." });
        }

        if (!double.TryParse(latitudeString, out double latitude) || !double.TryParse(longitudeString, out double longitude))
        {
            _logger.LogError($" Error: Invalid coordinate format. Latitude: {latitudeString}, Longitude: {longitudeString}");
            return Json(new { success = false, message = "Invalid coordinates. Use numbers only." });
        }

        // Convert to GeoPoint
        var geoPoint = new GeoPoint(latitude, longitude);
        _logger.LogInformation($" Parsed GeoPoint: ({geoPoint.Latitude}, {geoPoint.Longitude})");

        //  Fix: Save event with exact values
        bool success = await _eventsDb.CreateEventAsync(
            newEvent.EventName,
            newEvent.EventCategory,
            newEvent.EventDescription,
            selectedEventDate,
            newEvent.EventLocation,
            newEvent.EventImageURL,
            newEvent.EventMaxAttendees,
            (int)Math.Round(newEvent.EventRatings),
            geoPoint
        );

        if (success)
        {
            return Json(new { success = true, message = " Event created successfully!" });
        }
        else
        {
            return Json(new { success = false, message = " Failed to create event." });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($" Error creating event: {ex.Message}");
        return Json(new { success = false, message = "An error occurred while creating the event." });
    }
}

[HttpPost]
public async Task<IActionResult> UploadEventImage(IFormFile eventImage)
{
    try
    {
        if (eventImage == null || eventImage.Length == 0)
        {
            return Json(new { success = false, message = "Please select an image file." });
        }

        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = $"{Guid.NewGuid()}_{eventImage.FileName}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await eventImage.CopyToAsync(fileStream);
        }

        // Return the image URL
        string imageUrl = $"/Images/{uniqueFileName}";
        return Json(new { success = true, imageUrl = imageUrl });
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error uploading event image: {ex.Message}");
        return Json(new { success = false, message = "An error occurred while uploading the image." });
    }
}
        
        // GET: Load Edit Event Page
[HttpGet]
public async Task<IActionResult> EditEvent(int eventId)
{
    try
    {
        _logger.LogInformation($"🟢 Fetching event with ID: {eventId}");

        var eventToEdit = await _eventsDb.GetEventByIdAsync(eventId);

        if (eventToEdit == null)
        {
            _logger.LogError($"❌ Event with ID {eventId} not found or data is missing.");
            TempData["ErrorMessage"] = "❌ Event not found.";
            return RedirectToAction("AdminHome"); // Redirect to avoid null reference errors
        }

        _logger.LogInformation($"✅ Event found: {eventToEdit.EventName}");
        return View("EditEvent", eventToEdit);
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error fetching event for editing: {ex.Message}");
        TempData["ErrorMessage"] = "An error occurred while loading the event.";
        return RedirectToAction("AdminHome");
    }
}
  //  POST: Update Event (Ensures Image is Required)
[HttpPost]
public async Task<IActionResult> EditEvent(EventViewModel updatedEvent, IFormFile eventImage)
{
    if (!ModelState.IsValid)
    {
        return View(updatedEvent);
    }

    try
    {
        // 🔹 Require an Image to Proceed
        if (eventImage == null || eventImage.Length == 0)
        {
            TempData["ErrorMessage"] = "❌ You must select an image before updating.";
            return View(updatedEvent);
        }

        // Define the image storage path inside wwwroot/Images
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // Generate a unique file name
        string uniqueFileName = $"{Guid.NewGuid()}_{eventImage.FileName}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        // ⬆ Save the image file to wwwroot/Images
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await eventImage.CopyToAsync(fileStream);
        }

        //  Update the EventImageURL to use local path
        updatedEvent.EventImageURL = $"/Images/{uniqueFileName}";

        //  Update Firestore with new event details (including the local image URL)
        bool success = await _eventsDb.UpdateEventAsync(updatedEvent);

        if (success)
        {
            TempData["SuccessMessage"] = " Event updated successfully!";
            return RedirectToAction("AdminEventsManagement");
        }
        else
        {
            TempData["ErrorMessage"] = "❌ Failed to update event.";
            return View(updatedEvent);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event: {ex.Message}");
        TempData["ErrorMessage"] = "An error occurred while updating the event.";
        return View(updatedEvent);
    }
}
        // ✅ GET: Admin Event Management Page (With VisibilityStatus Filtering)
  [HttpGet]
public async Task<IActionResult> AdminEventManagement(string[] status)
{
    try
    {
        // Default to showing "Online" events if no filter is applied
        List<string> selectedStatus = (status != null && status.Length > 0)
            ? status.ToList()
            : new List<string> { "Online" }; 

        var events = await _eventsDb.GetAllEventsAsync();
        
        // Debug log all events before filtering
        foreach (var evt in events)
        {
            _logger.LogInformation($"Retrieved event: ID={evt.EventID}, Name={evt.EventName}, Status={evt.Status}");
        }

        if (events == null || events.Count == 0) 
        {
            _logger.LogWarning("⚠️ No events found in Firestore.");
            events = new List<EventViewModel>();
        }

        // No filtering - show all events for debugging
        var filteredEvents = events.ToList();

        _logger.LogInformation($" Total events: {events.Count}, Filtered events: {filteredEvents.Count}");
        
        // Log status options
        var statusOptions = new List<string> { "Online", "Previous", "Deleted" };
        _logger.LogInformation($"Status options: {string.Join(", ", statusOptions)}");
        _logger.LogInformation($"Selected status: {string.Join(", ", selectedStatus)}");

        ViewBag.Events = filteredEvents; // Pass all events to view for now
        ViewBag.Categories = filteredEvents
            .Select(e => e.EventCategory)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        ViewBag.StatusOptions = statusOptions;
        ViewBag.SelectedStatus = selectedStatus;

        return View(filteredEvents); // Pass events as model
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error in AdminEventManagement: {ex.Message}");
        TempData["ErrorMessage"] = "Error loading events.";
        return RedirectToAction("AdminHome");
    }
}

//  controller action to use the new model
[HttpPost]
public async Task<IActionResult> UpdateEventStatus([FromBody] EventStatusUpdateModel request)
{
    try
    {
        if (request.EventId <= 0)
        {
            _logger.LogWarning($"❌ Invalid Event ID: {request.EventId}");
            return BadRequest(new { success = false, error = "Valid Event ID is required." });
        }

        Query eventsQuery = _firestoreDb.Collection("EventsCollection")
            .WhereEqualTo("EventID", request.EventId);
        QuerySnapshot querySnapshot = await eventsQuery.GetSnapshotAsync();

        if (querySnapshot.Documents.Count == 0)
        {
            _logger.LogWarning($"❌ Event not found with ID: {request.EventId}");
            return NotFound(new { success = false, error = "Event not found." });
        }

        DocumentReference eventRef = querySnapshot.Documents[0].Reference;

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "Status", request.Status }
        };

        await eventRef.UpdateAsync(updates);
        
        _logger.LogInformation($" Event {request.EventId} status updated to: {request.Status}");
        return Ok(new { 
            success = true, 
            message = $"Event status updated to {request.Status}." 
        });
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event status: {ex.Message}");
        return BadRequest(new { success = false, error = ex.Message });
    }
}

[HttpGet]
public async Task<IActionResult> GetEventById(int eventId)
{
    var eventToEdit = await _eventsDb.GetEventByIdAsync(eventId);

    if (eventToEdit == null)
    {
        return Json(new { success = false, error = "Event not found." });
    }

    return Json(new { success = true, eventToEdit });
}


//Edit Form code
[HttpPost]
public async Task<IActionResult> UpdateEventDetails(EventViewModel updatedEvent)
{
    if (!ModelState.IsValid)
    {
        return Json(new { success = false, message = "Invalid form data" });
    }

    try
    {
        _logger.LogInformation($"🔄 Updating event: {updatedEvent.EventID}");

        // Convert event date to Firestore timestamp
        var eventTimestamp = Timestamp.FromDateTime(updatedEvent.GetEventDateAsDateTime().ToUniversalTime());

        // 🔹 Update the event in EventsCollection
        Dictionary<string, object> updatedData = new Dictionary<string, object>
        {
            { "EventName", updatedEvent.EventName },
            { "EventCategory", updatedEvent.EventCategory },
            { "EventDescription", updatedEvent.EventDescription },
            { "EventImageURL", updatedEvent.EventImageURL },
            { "EventLocation", updatedEvent.EventLocation },
            { "EventMaxAttendees", updatedEvent.EventMaxAttendees },
            { "EventRatings", updatedEvent.EventRatings },
            { "EventDate", eventTimestamp }, // ✅ Update Event Date
            { "Status", updatedEvent.Status }
        };

        DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(updatedEvent.EventID.ToString());
        await eventRef.UpdateAsync(updatedData);

        _logger.LogInformation($" Event '{updatedEvent.EventName}' updated successfully.");

        // 🔹 Update RSVPCollection StartDate to match updated EventDate
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", updatedEvent.EventID);
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
        {
            DocumentReference rsvpRef = rsvp.Reference;
            await rsvpRef.UpdateAsync(new Dictionary<string, object>
            {
                { "StartDate", eventTimestamp }  // Ensure StartDate matches updated EventDate
            });
        }

        _logger.LogInformation($" Updated all RSVP StartDates for Event ID {updatedEvent.EventID}.");

        return Json(new { success = true, message = "Event details updated successfully!" });
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event details: {ex.Message}");
        return Json(new { success = false, message = "An error occurred while updating the event" });
    }
}

[HttpPost]
public async Task<IActionResult> UpdateEventImage(int eventId, IFormFile eventImage)
{
    try
    {
        if (eventImage == null || eventImage.Length == 0)
        {
            TempData["ErrorMessage"] = "❌ Please select an image file.";
            return RedirectToAction("EditEvent", new { eventId = eventId });
        }

        // 🔹 Retrieve the event from Firestore
        var currentEvent = await _eventsDb.GetEventByIdAsync(eventId);
        if (currentEvent == null)
        {
            TempData["ErrorMessage"] = "❌ Event not found.";
            return RedirectToAction("AdminEventsManagement");
        }

        // 🔹 Save new image file
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(eventImage.FileName)}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await eventImage.CopyToAsync(fileStream);
        }

        // 🔹 Update event image URL in Firestore
        currentEvent.EventImageURL = $"/Images/{uniqueFileName}";

        // 🔹 Ensure Firestore update succeeds
        bool success = await _eventsDb.UpdateEventAsync(currentEvent);
        if (!success)
        {
            TempData["ErrorMessage"] = "❌ Failed to update event image in Firestore.";
            return RedirectToAction("EditEvent", new { eventId = eventId });
        }

        TempData["SuccessMessage"] = "✅ Event image updated successfully!";
        return RedirectToAction("EditEvent", new { eventId = eventId });
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event image: {ex.Message}");
        TempData["ErrorMessage"] = "An error occurred while updating the event image.";
        return RedirectToAction("EditEvent", new { eventId = eventId });
    }
}

[HttpPost]
public async Task<IActionResult> DeleteEvent([FromBody] Dictionary<string, object> requestData)
{
    try
    {
        if (!requestData.ContainsKey("eventId"))
        {
            return Json(new { success = false, error = "❌ Missing eventId parameter." });
        }

        int eventId = Convert.ToInt32(requestData["eventId"]);

        // ✅ Check if event exists
        var eventToDelete = await _eventsDb.GetEventByIdAsync(eventId);
        if (eventToDelete == null)
        {
            return Json(new { success = false, error = "❌ Event not found." });
        }

        // ✅ Soft delete by updating status in Firestore
        bool success = await _eventsDb.UpdateEventStatusAsync(eventId, "Deleted");

        if (success)
        {
            return Json(new { success = true });
        }
        else
        {
            return Json(new { success = false, error = "❌ Failed to delete event in Firestore." });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error deleting event: {ex.Message}");
        return Json(new { success = false, error = "❌ Server error occurred while deleting event." });
    }
}
}
}