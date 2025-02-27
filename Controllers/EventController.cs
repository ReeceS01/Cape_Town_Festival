using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using Cape_Town_Festival.Database;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Cape_Town_Festival.Models; 
using Cape_Town_Festival.Services;

namespace Cape_Town_Festival.Controllers
{
    public class EventsController : Controller
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly EventsDatabase _eventsDb;
        private readonly ILogger<EventsController> _logger;
        private readonly WeatherService _weatherService;

        public EventsController(FirestoreDb firestoreDb, EventsDatabase eventsDb, ILogger<EventsController> logger, WeatherService weatherService) 
        {
            _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
            _eventsDb = eventsDb ?? throw new ArgumentNullException(nameof(eventsDb));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        }

        // Load All Events Page
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Fetching all events from Firestore.");
                var events = await FetchEventsWithSeatsLeft();

                // Add weather data to events
                await AddWeatherDataToEvents(events);

                ViewBag.Events = events;
                return View("Events");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading events: {ex.Message}");
                ViewBag.Error = "Failed to load events.";
                return View("Error");
            }
        }

        // Load Dynamic Events Page
        [HttpGet]
        public async Task<IActionResult> EventsDynamic()
        {
            try
            {
                _logger.LogInformation("Fetching dynamic events from Firestore.");
                var events = await FetchEventsWithSeatsLeft();

                        // DEBUG: Test direct API call
                        var testWeather = await _weatherService.TestDirectApiCall();
                        _logger.LogInformation($"Test weather call result: {testWeather.Description}, {testWeather.Temperature}°C");
                        
                        // Debug: Log first event location
                        if (events.Count > 0)
                        {
                            var firstEvent = events[0];
                            if (firstEvent.ContainsKey("EventLocation"))
                            {
                                string location = firstEvent["EventLocation"].ToString();
                                _logger.LogInformation($"First event location format: '{location}'");
                            }
                        }


                // Add weather data to events
                await AddWeatherDataToEvents(events);

                ViewBag.Events = events;
                return View("EventsDynamic");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading dynamic events: {ex.Message}");
                ViewBag.Error = "Failed to load events.";
                return View("Error");
            }
        }

        // Load "Learn More" Page for Specific Event
        [HttpGet]
        public async Task<IActionResult> LearnMore(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                _logger.LogError("LearnMore called with missing eventId");
                return NotFound("Event ID is required.");
            }

            try
            {
                _logger.LogInformation($"Fetching event details for eventId: {eventId}");

                // Fetch event details from Firestore
                DocumentReference docRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    _logger.LogWarning($"Event not found in Firestore: {eventId}");
                    return NotFound("Event not found.");
                }

                Dictionary<string, object> eventDetails = snapshot.ToDictionary();
                eventDetails["EventID"] = eventId;

                // Fetch the correct Seats Left using the RSVP logic
                int seatsLeft = await GetSeatsLeft(eventId);
                eventDetails["SeatsLeft"] = seatsLeft;

                // Pass updated event details to the view
                ViewBag.Event = eventDetails;

                return View("LearnMore");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching event details: {ex.Message}");
                return View("Error");
            }
        }

        // Fetch All Events & Calculate Seats Left Using RSVP's Calculation
        private async Task<List<Dictionary<string, object>>> FetchEventsWithSeatsLeft()
        {
            var events = await _eventsDb.GetEventsAsync();

            foreach (var eventItem in events)
            {
                string eventId = eventItem.ContainsKey("EventID") ? eventItem["EventID"].ToString() : string.Empty;

                if (!string.IsNullOrEmpty(eventId))
                {
                    int seatsLeft = await GetSeatsLeft(eventId);
                    eventItem["SeatsLeft"] = seatsLeft;
                }
                else
                {
                    eventItem["SeatsLeft"] = 0;
                    _logger.LogWarning("Missing or null EventID in Firestore data.");
                }
            }

            return events;
        }

        // Get "Seats Left" Calculation (Uses the Same Logic as RSVPController)
       private async Task<int> GetSeatsLeft(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                _logger.LogWarning("GetSeatsLeft called with null or empty eventId.");
                return 0; // Return 0 if eventId is invalid
            }

            // 🔹 Fetch the correct event from Firestore
            DocumentReference docRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                _logger.LogWarning($"Event not found in Firestore: {eventId}");
                return 0;
            }

            var eventData = snapshot.ToDictionary();

            // Ensure "EventMaxAttendees" is properly retrieved
            int maxAttendees = eventData.ContainsKey("EventMaxAttendees") 
                && eventData["EventMaxAttendees"] != null
                ? Convert.ToInt32(eventData["EventMaxAttendees"])
                : 0;  // Default to 0 if missing or null

            // 🔹 Fetch RSVPs for this Event
            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection")
                .WhereEqualTo("EventID", eventId);
            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

            int totalAttendees = rsvpSnapshot.Documents
                .Where(rsvp => rsvp.TryGetValue("AttendeeCount", out object attendeeObj) && attendeeObj is long)
                .Sum(rsvp => (int)(long)rsvp.GetValue<long>("AttendeeCount"));

            //  Correct "Seats Left" Calculation
            int seatsLeft = Math.Max(0, maxAttendees - totalAttendees);
            _logger.LogInformation($"Seats Left for eventId {eventId}: {seatsLeft} (Max: {maxAttendees}, RSVPs: {totalAttendees})");

            return seatsLeft;
        }

        public async Task<IActionResult> EditEvent(int eventId)
        {
            if (eventId <= 0)
            {
                _logger.LogError("EditEvent called with invalid eventId");
                return NotFound("Valid Event ID is required.");
            }

            try
            {
                // Fetch event details using the Database layer
                var eventDetails = await _eventsDb.GetEventByIdAsync(eventId);

                if (eventDetails == null)
                {
                    _logger.LogWarning($"Event not found in Firestore: {eventId}");
                    return NotFound("Event not found.");
                }

                return View("EditEvents", eventDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading event details: {ex.Message}");
                return View("Error");
            }
        }



    // New method to add weather data to a list of events
        private async Task AddWeatherDataToEvents(List<Dictionary<string, object>> events)
        {
            foreach (var eventItem in events)
            {
                await AddWeatherDataToEvent(eventItem);
            }
        }
        // Updated method to add weather data to a single event
private async Task AddWeatherDataToEvent(Dictionary<string, object> eventItem)
{
    try
    {
        // Only proceed if the event contains the necessary data
        if (eventItem.ContainsKey("EventDate"))
        {
            string locationForWeather = "";
            
            // First try to use EventLatLong if it exists
            if (eventItem.ContainsKey("EventLatLong"))
            {
                var latLong = eventItem["EventLatLong"].ToString();
                
                // Format: [33.92125645743071° S, 18.41834072883623° E]
                // Need to extract just the numbers and handle S/W as negative
                latLong = latLong.Replace("[", "").Replace("]", "");
                var parts = latLong.Split(',');
                
                if (parts.Length == 2)
                {
                    // Parse latitude (first part)
                    var latPart = parts[0].Trim();
                    double lat = 0;
                    bool isNegativeLat = latPart.Contains("S");
                    latPart = latPart.Replace("°", "").Replace("S", "").Replace("N", "").Trim();
                    if (double.TryParse(latPart, out lat) && isNegativeLat)
                    {
                        lat = -lat; // South is negative
                    }
                    
                    // Parse longitude (second part)
                    var lngPart = parts[1].Trim();
                    double lng = 0;
                    bool isNegativeLng = lngPart.Contains("W");
                    lngPart = lngPart.Replace("°", "").Replace("E", "").Replace("W", "").Trim();
                    if (double.TryParse(lngPart, out lng) && isNegativeLng)
                    {
                        lng = -lng; // West is negative
                    }
                    
                    // Format as "latitude,longitude" for the weather API
                    locationForWeather = $"{lat},{lng}";
                    _logger.LogInformation($"Using coordinates for weather: {locationForWeather}");
                }
            }
            // Fall back to location string if coordinates not available
            else if (eventItem.ContainsKey("EventLocation"))
            {
                locationForWeather = eventItem["EventLocation"].ToString();
                _logger.LogInformation($"Using location name for weather: {locationForWeather}");
            }
            
            var eventDate = ((Google.Cloud.Firestore.Timestamp)eventItem["EventDate"]).ToDateTime();
            
            // Get weather data for this event
            var weatherData = await _weatherService.GetWeatherForLocationAsync(locationForWeather, eventDate);
            
            // Add weather data to the event dictionary
            eventItem["Weather"] = new Dictionary<string, object>
            {
                { "Temperature", weatherData.Temperature },
                { "Description", weatherData.Description },
                { "IconUrl", weatherData.IconUrl }
            };
            
            _logger.LogInformation($"Added weather data for event: {eventItem["EventName"]} - {weatherData.Description}, {weatherData.Temperature}°C");
        }
        else
        {
            _logger.LogWarning("Event missing date data, cannot fetch weather");
            
            // Add default weather data
            eventItem["Weather"] = new Dictionary<string, object>
            {
                { "Temperature", 0 },
                { "Description", "Weather data unavailable" },
                { "IconUrl", "" }
            };
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error fetching weather data: {ex.Message}");
        
        // Add default weather data on error
        eventItem["Weather"] = new Dictionary<string, object>
        {
            { "Temperature", 0 },
            { "Description", "Weather data unavailable" },
            { "IconUrl", "" }
        };
    }
}
       

    }
}