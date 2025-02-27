using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cape_Town_Festival.Models;


namespace Cape_Town_Festival.Database
{
    public class EventsDatabase
    {
        private readonly FirestoreDb _firestoreDb;
             private readonly ILogger<EventsDatabase> _logger;

        public EventsDatabase(FirestoreDb firestoreDb, ILogger<EventsDatabase> logger)
        {
            _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // ✅ Initialize logger
        }

       public async Task<List<Dictionary<string, object>>> GetEventsAsync()
{
    try
    {
        HashSet<int> seenEventIds = new HashSet<int>(); // Track seen EventIDs
        List<Dictionary<string, object>> eventsList = new List<Dictionary<string, object>>();

        Query eventsQuery = _firestoreDb.Collection("EventsCollection");
        QuerySnapshot eventsSnapshot = await eventsQuery.GetSnapshotAsync();

        foreach (DocumentSnapshot doc in eventsSnapshot.Documents)
        {
            var eventData = doc.ToDictionary();
            if (!eventData.ContainsKey("EventID")) continue;

            int eventId = eventData.ContainsKey("EventID") && int.TryParse(eventData["EventID"]?.ToString(), out int parsedEventId) ? parsedEventId : 0;

            if (seenEventIds.Contains(eventId)) continue;
            seenEventIds.Add(eventId);

            DateTime eventDate = eventData.ContainsKey("EventDate") && eventData["EventDate"] is Google.Cloud.Firestore.Timestamp ts
                ? ts.ToDateTime()
                : DateTime.MinValue;

            // Changed from VisibleStatus to Status
            string currentStatus = eventData.ContainsKey("Status") ? eventData["Status"]?.ToString() ?? "Online" : "Online";

            // Auto-change event status to "Previous" if past event
            if (eventDate < DateTime.UtcNow && currentStatus == "Online")
            {
                currentStatus = "Previous";
                await _firestoreDb.Collection("EventsCollection").Document(eventId.ToString())
                    .UpdateAsync(new Dictionary<string, object> { { "Status", "Previous" } }); // Updated field name
            }

            eventData["EventID"] = eventId;
            eventData["Status"] = currentStatus; // Changed from VisibleStatus to Status

            // Fetch RSVP count for the event
            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", eventId);
            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

            int totalAttendees = 0;
            foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
            {
                if (rsvp.TryGetValue("AttendeeCount", out object attendeeObj) && attendeeObj is long attendeeCount)
                {
                    totalAttendees += (int)attendeeCount;
                }
            }

            // Calculate available seats
            int maxAttendees = eventData.ContainsKey("EventMaxAttendees") && int.TryParse(eventData["EventMaxAttendees"]?.ToString(), out int maxSeats)
                ? maxSeats
                : 0;
            int seatsLeft = Math.Max(0, maxAttendees - totalAttendees);

            // Assign final values with null-checking
            eventData["SeatsLeft"] = seatsLeft;
            eventData["EventName"] = eventData.ContainsKey("EventName") ? eventData["EventName"]?.ToString() ?? "Unknown" : "Unknown";
            eventData["EventCategory"] = eventData.ContainsKey("EventCategory") ? eventData["EventCategory"]?.ToString() ?? "Unknown" : "Unknown";
            eventData["EventDescription"] = eventData.ContainsKey("EventDescription") ? eventData["EventDescription"]?.ToString() ?? "No Description" : "No Description";
            eventData["EventImageURL"] = eventData.ContainsKey("EventImageURL") ? eventData["EventImageURL"]?.ToString() ?? "/images/default-placeholder.png" : "/images/default-placeholder.png";
            eventData["EventLatLong"] = eventData.ContainsKey("EventLatLong") ? eventData["EventLatLong"]?.ToString() ?? "" : "";
            eventData["EventLocation"] = eventData.ContainsKey("EventLocation") ? eventData["EventLocation"]?.ToString() ?? "Unknown" : "Unknown";

            eventsList.Add(eventData);
        }

        return eventsList;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error fetching events: {ex.Message}");
        return new List<Dictionary<string, object>>();
    }
}

 public async Task<List<EventViewModel>> GetAllEventsAsync()
{
    try
    {
        List<EventViewModel> eventsList = new List<EventViewModel>();
        QuerySnapshot eventsSnapshot = await _firestoreDb.Collection("EventsCollection").GetSnapshotAsync();

        foreach (DocumentSnapshot doc in eventsSnapshot.Documents)
        {
            var eventData = doc.ToDictionary();
            if (!eventData.TryGetValue("EventID", out object? eventIdObj) || eventIdObj == null || !int.TryParse(eventIdObj.ToString(), out int eventId))
            {
                _logger.LogWarning("⚠️ Skipping event: Missing or invalid EventID.");
                continue;
            }

            // ✅ Get status
            string status = eventData.ContainsKey("Status") ? eventData["Status"]?.ToString() ?? "Online" : "Online";

            // ✅ Convert Firestore Timestamp to DateTime
            DateTime eventDate;
            if (eventData.ContainsKey("EventDate") && eventData["EventDate"] is Google.Cloud.Firestore.Timestamp ts)
            {
                eventDate = ts.ToDateTime().ToUniversalTime();
            }
            else
            {
                eventDate = DateTime.UtcNow; // Default if missing
            }

            // ✅ Auto-update status to "Previous" for past events
            if (eventDate < DateTime.UtcNow && status == "Online")
            {
                status = "Previous";
                await doc.Reference.UpdateAsync("Status", "Previous");
            }

            // ✅ Fetch RSVP count
            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", eventId);
            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();
            int totalRSVPs = rsvpSnapshot.Documents.Count;

            // ✅ Convert Firestore GeoPoint safely
            Google.Cloud.Firestore.GeoPoint geoPoint = new Google.Cloud.Firestore.GeoPoint(0.0, 0.0);
            if (eventData.ContainsKey("EventLatLong") && eventData["EventLatLong"] is Google.Cloud.Firestore.GeoPoint gp)
            {
                geoPoint = gp;
            }
            string eventLatLong = $"{geoPoint.Latitude},{geoPoint.Longitude}";

            // ✅ Populate event model
            eventsList.Add(new EventViewModel
            {
                EventID = eventId,
                EventName = eventData.ContainsKey("EventName") ? eventData["EventName"]?.ToString() ?? "Unknown" : "Unknown",
                EventCategory = eventData.ContainsKey("EventCategory") ? eventData["EventCategory"]?.ToString() ?? "Uncategorized" : "Uncategorized",
                EventDescription = eventData.ContainsKey("EventDescription") ? eventData["EventDescription"]?.ToString() ?? "No Description" : "No Description",
                EventImageURL = eventData.ContainsKey("EventImageURL") ? eventData["EventImageURL"]?.ToString() ?? "/images/default-placeholder.png" : "/images/default-placeholder.png",
                EventLatLong = geoPoint, // ✅ Correctly assign GeoPoint
                EventLocation = eventData.ContainsKey("EventLocation") ? eventData["EventLocation"]?.ToString() ?? "Unknown" : "Unknown",
                EventMaxAttendees = eventData.ContainsKey("EventMaxAttendees") && int.TryParse(eventData["EventMaxAttendees"]?.ToString(), out int maxAttendees) ? maxAttendees : 0,
                EventRatings = eventData.ContainsKey("EventRatings") && double.TryParse(eventData["EventRatings"]?.ToString(), out double ratings) ? ratings : 0.0,
                EventDate = Google.Cloud.Firestore.Timestamp.FromDateTime(eventDate), // ✅ Correct Timestamp conversion
                Status = status,
                EventRSVPs = totalRSVPs
            });

            _logger.LogInformation($"✅ Loaded event: {eventId} with status: {status}");
        }

        return eventsList;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error fetching events: {ex.Message}");
        return new List<EventViewModel>();
    }
}

// ✅ Create a New Event (Admins Only)
public async Task<bool> CreateEventAsync(
    string eventName, 
    string eventCategory, 
    string eventDescription, 
    DateTime eventDate, 
    string eventLocation, 
    string eventImageURL, 
    int eventMaxAttendees, 
    int eventRatings,
    GeoPoint eventLatLong)  // Changed to accept GeoPoint
{
    try
    {
        Query eventsQuery = _firestoreDb.Collection("EventsCollection").OrderByDescending("EventID").Limit(1);
        QuerySnapshot eventsSnapshot = await eventsQuery.GetSnapshotAsync();

        int nextEventId = eventsSnapshot.Documents.Count > 0
            ? eventsSnapshot.Documents.First().GetValue<int>("EventID") + 1
            : 1;

        // Create event data with exact date and GeoPoint
        Dictionary<string, object> eventData = new Dictionary<string, object>
        {
            { "EventID", nextEventId },
            { "EventName", eventName },
            { "EventCategory", eventCategory ?? "Uncategorized" },
            { "EventDescription", eventDescription },
            { "EventDate", Timestamp.FromDateTime(eventDate) },  // Use exact date from form
            { "EventLocation", eventLocation },
            { "EventImageURL", eventImageURL ?? "" },
            { "EventMaxAttendees", eventMaxAttendees },
            { "EventRatings", eventRatings },
            { "EventLatLong", eventLatLong },  // Store GeoPoint directly
            { "Status", "Online" }
        };

        _logger.LogInformation($"Creating event with date: {eventDate}, coordinates: ({eventLatLong.Latitude}, {eventLatLong.Longitude})");

        await _firestoreDb.Collection("EventsCollection").Document(nextEventId.ToString()).SetAsync(eventData);
        _logger.LogInformation($"✅ New Event Created with ID: {nextEventId}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error creating event: {ex.Message}");
        return false;
    }
}

// ✅ Edit an Event (Admins Only)
public async Task<bool> EditEventAsync(string eventId, string eventName, string eventCategory, string eventDescription, 
                                       DateTime eventDate, string eventLocation, string eventImageURL, 
                                       int eventMaxAttendees, int eventRatings, string eventLatLong, string status)
{
    try
    {
        _logger.LogInformation($"🔄 Editing event: {eventId} - {eventName}");
        
        DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId);

        // ✅ Convert eventLatLong string to GeoPoint
        GeoPoint geoPoint;
        if (!string.IsNullOrWhiteSpace(eventLatLong))
        {
            var coordinates = eventLatLong.Split(',');
            if (coordinates.Length == 2 &&
                double.TryParse(coordinates[0], out double latitude) &&
                double.TryParse(coordinates[1], out double longitude))
            {
                geoPoint = new Google.Cloud.Firestore.GeoPoint(latitude, longitude);
            }
            else
            {
                geoPoint = new Google.Cloud.Firestore.GeoPoint(0.0, 0.0);
            }
        }
        else
        {
            geoPoint = new Google.Cloud.Firestore.GeoPoint(0.0, 0.0);
        }

        Dictionary<string, object> updatedData = new Dictionary<string, object>
        {
            { "EventName", eventName },
            { "EventCategory", eventCategory },
            { "EventDescription", eventDescription },
            { "EventDate", Google.Cloud.Firestore.Timestamp.FromDateTime(eventDate.ToUniversalTime()) }, // ✅ Fix Timestamp conversion
            { "EventLocation", eventLocation },
            { "EventImageURL", eventImageURL },
            { "EventMaxAttendees", eventMaxAttendees },
            { "EventRatings", eventRatings },
           { "EventLatLong", eventLatLong }, // Store the GeoPoint directly
            { "Status", status }
        };

        await eventRef.UpdateAsync(updatedData);
        _logger.LogInformation($"✅ Event '{eventName}' updated successfully.");

        // Fetch all RSVPs related to this event
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", int.Parse(eventId));
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        // Update StartDate in all RSVPs for this Event
        foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
        {
            DocumentReference rsvpRef = rsvp.Reference;
            await rsvpRef.UpdateAsync("StartDate", Timestamp.FromDateTime(eventDate.ToUniversalTime()));
        }

        _logger.LogInformation($"✅ All RSVPs updated with new StartDate: {eventDate}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event {eventId}: {ex.Message}");
        return false;
    }
}  

public async Task<bool> UpdateEventAsync(EventViewModel updatedEvent)
{
    try
    {
        _logger.LogInformation($"🔄 Updating event: {updatedEvent.EventID}");

        var eventRef = _firestoreDb.Collection("EventsCollection").Document(updatedEvent.EventID.ToString());

        // Convert event date to Firestore timestamp
        var eventTimestamp = Timestamp.FromDateTime(updatedEvent.GetEventDateAsDateTime().ToUniversalTime());

        await eventRef.UpdateAsync(new Dictionary<string, object>
        {
            { "EventName", updatedEvent.EventName },
            { "EventCategory", updatedEvent.EventCategory },
            { "EventDescription", updatedEvent.EventDescription },
            { "EventImageURL", updatedEvent.EventImageURL },
            { "EventLocation", updatedEvent.EventLocation },
            { "EventMaxAttendees", updatedEvent.EventMaxAttendees },
            { "EventRatings", updatedEvent.EventRatings },
            { "EventDate", eventTimestamp }, // ✅ Ensure EventDate updates properly
            { "Status", updatedEvent.Status }
        });

        _logger.LogInformation($"✅ Successfully updated event: {updatedEvent.EventID}");

        // 🔹 Update RSVPCollection StartDate to match updated EventDate
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("EventID", updatedEvent.EventID);
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        foreach (DocumentSnapshot rsvp in rsvpSnapshot.Documents)
        {
            DocumentReference rsvpRef = rsvp.Reference;
            await rsvpRef.UpdateAsync(new Dictionary<string, object>
            {
                { "StartDate", eventTimestamp }
            });
        }

        _logger.LogInformation($"✅ All RSVPs updated with new StartDate: {eventTimestamp.ToDateTime()}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event {updatedEvent.EventID}: {ex.Message}");
        return false;
    }
}

// ✅ Fix for GetEventByIdAsync
public async Task<EventViewModel?> GetEventByIdAsync(int eventId)
{
    _logger.LogInformation($"🔍 Searching for event with ID: {eventId}");

    var docRef = _firestoreDb.Collection("EventsCollection").Document(eventId.ToString());
    var doc = await docRef.GetSnapshotAsync();

    if (!doc.Exists)
    {
        _logger.LogError($"❌ Event with ID {eventId} not found in Firestore.");
        return null;
    }

    var eventData = doc.ToDictionary();
    if (eventData == null)
    {
        _logger.LogError($"❌ Event data for ID {eventId} is null.");
        return null;
    }

    try
    {
        // ✅ Fetch Status and auto-update if needed
        string status = eventData.ContainsKey("Status") ? eventData["Status"]?.ToString() ?? "Online" : "Online";
        Timestamp eventTimestamp = eventData.ContainsKey("EventDate") && eventData["EventDate"] is Timestamp ts
            ? ts
            : Timestamp.FromDateTime(DateTime.UtcNow);

        if (eventTimestamp.ToDateTime() < DateTime.UtcNow && status == "Online")
        {
            status = "Previous";
            await docRef.UpdateAsync("Status", "Previous");
        }

        // ✅ Extract Latitude & Longitude from Firestore GeoPoint
        string latitude = "0", longitude = "0";
        if (eventData.ContainsKey("EventLatLong") && eventData["EventLatLong"] is GeoPoint geoPoint)
        {
            latitude = geoPoint.Latitude.ToString();
            longitude = geoPoint.Longitude.ToString();
        }

        // ✅ Ensure Firestore data is correctly assigned to the model
        return new EventViewModel
        {
            EventID = eventId,
            EventName = eventData["EventName"]?.ToString() ?? "Unknown",
            EventCategory = eventData["EventCategory"]?.ToString() ?? "Uncategorized",
            EventDescription = eventData["EventDescription"]?.ToString() ?? "No Description",
            EventImageURL = eventData["EventImageURL"]?.ToString() ?? "/images/default-placeholder.png",
            EventLatLong = new GeoPoint(double.Parse(latitude), double.Parse(longitude)),  // ✅ Correctly assign GeoPoint
            EventLocation = eventData["EventLocation"]?.ToString() ?? "Unknown",
            EventMaxAttendees = eventData.ContainsKey("EventMaxAttendees") ? Convert.ToInt32(eventData["EventMaxAttendees"]) : 0,
            EventRatings = eventData.ContainsKey("EventRatings") ? Convert.ToDouble(eventData["EventRatings"]) : 0,
            EventDate = eventTimestamp,  // ✅ Correct Firestore Timestamp conversion
            Status = status
        };
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error parsing event data: {ex.Message}", ex);
        return null;
    }
}

public async Task<List<User>> GetRecentUsersAsync(int limit = 10)
{
    try
    {
        _logger.LogInformation($"🔍 Fetching {limit} recent users");
        
        var snapshot = await _firestoreDb
            .Collection("UserCollection")
            .OrderByDescending("Birthdate")
            .Limit(limit)
            .GetSnapshotAsync();

        var users = snapshot.Documents
            .Select(d => d.ConvertTo<User>())
            .ToList();

        _logger.LogInformation($"✅ Successfully fetched {users.Count} users");
        return users;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error fetching recent users: {ex.Message}");
        return new List<User>();
    }
}

public async Task<int> GetTotalUsersCountAsync()
{
    try
    {
        _logger.LogInformation("🔍 Getting total user count");
        
        var snapshot = await _firestoreDb
            .Collection("UserCollection")
            .GetSnapshotAsync();

        var count = snapshot.Documents.Count;
        _logger.LogInformation($"✅ Total users count: {count}");
        return count;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error getting user count: {ex.Message}");
        return 0;
    }
}

//  NEW: Dashboard Fetch all users
        public async Task<List<User>> GetAllUsersAsync()
{
    var usersCollection = _firestoreDb.Collection("Users");
    var snapshot = await usersCollection.GetSnapshotAsync();

    return snapshot.Documents
        .Select(doc =>
        {
            var user = doc.ConvertTo<User>();

            //  Ensure CreatedAt is parsed correctly
            if (doc.ContainsField("CreatedAt"))
            {
                user.CreatedAt = doc.GetValue<Timestamp>("CreatedAt");
            }
            else
            {
                user.CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow); // Set default value if missing
            }

            return user;
        })
        .ToList();
}
       
public async Task<bool> UpdateEventStatusAsync(int eventId, string newStatus)
{
    try
    {
        var eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId.ToString());

        await eventRef.UpdateAsync(new Dictionary<string, object>
        {
            { "Status", newStatus } // ✅ Change status to "Deleted"
        });

        _logger.LogInformation($"✅ Event {eventId} status updated to: {newStatus}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Error updating event {eventId} status: {ex.Message}");
        return false;
    }
}
    }
}