using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Google.Cloud.Firestore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cape_Town_Festival.Models;
using Cape_Town_Festival.Database;

namespace Cape_Town_Festival.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Dashboard")]
    public class DashboardController : Controller
    {
    private readonly EventsDatabase _eventsDatabase;
     private readonly ILogger<DashboardController> _logger;

    private FirestoreDb GetFirestoreDb()
    {
        var firebaseCredentialsPath = "/Users/reecesheldon/Cape_Town_Festival/wwwroot/firebase-adminsdk.json";
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", firebaseCredentialsPath);

        return FirestoreDb.Create("cpt-festival");
    }

    public DashboardController(EventsDatabase eventsDatabase, ILogger<DashboardController> logger)
    {
        _eventsDatabase = eventsDatabase;
        _logger = logger; // ✅ Assign logger
    }

    [HttpGet]
    [Route("")]
    [Route("Index")] // Ensure /Admin/Dashboard works
    public async Task<IActionResult> Index()
        {
            try
            {
                // Fetch all events
                var events = await _eventsDatabase.GetAllEventsAsync();

                
                var viewModel = new DashboardViewModel
                {
                    TotalEvents = events.Count,
                    TotalUsers = await _eventsDatabase.GetTotalUsersCountAsync(),
                    AverageOccupancyRate = CalculateAverageOccupancy(events),
                    AverageEventRating = CalculateAverageRating(events),
                    PopularCategories = GetPopularCategories(events),
                    UpcomingEvents = GetUpcomingEvents(events),
                    RecentUsers = await _eventsDatabase.GetRecentUsersAsync(),
                    TopPerformingEvents = GetTopPerformingEvents(events),
                    UserGrowth = await GetUserGrowthData(),

                    // ✅ Firestore Data
                    VisitorsAttendingFestival = await GetVisitorsAttendingFestival(),
                    PastEventAttendees = await GetPastEventAttendees()
                };

                return View("~/Views/Admin/Dashboard.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel { RequestId = ex.Message });
            }
        }

        private double CalculateAverageOccupancy(List<EventViewModel> events)
        {
            if (!events.Any()) return 0;

            var occupancyRates = events.Select(evt =>
            {
                return evt.EventMaxAttendees > 0 
                    ? (double)evt.EventRSVPs / evt.EventMaxAttendees * 100 
                    : 0;
            });

            return Math.Round(occupancyRates.Average(), 1);
        }

        private double CalculateAverageRating(List<EventViewModel> events)
        {
            var ratings = events.Select(e => e.EventRatings);
            return ratings.Any() ? Math.Round(ratings.Average(), 1) : 0;
        }

        private List<EventCategory> GetPopularCategories(List<EventViewModel> events)
        {
            return events
                .GroupBy(e => e.EventCategory)
                .Select(g => new EventCategory
                {
                    Name = g.Key,
                    RSVPCount = g.Sum(e => e.EventRSVPs)
                })
                .OrderByDescending(c => c.RSVPCount)
                .Take(5)
                .ToList();
        }

        private List<EventViewModel> GetUpcomingEvents(List<EventViewModel> events)
        {
            return events
                .Where(e => e.GetEventDateAsDateTime() > DateTime.UtcNow && e.Status == "Online")
                .OrderBy(e => e.GetEventDateAsDateTime())
                .Take(5)
                .ToList();
        }

        //  NEW: Get top-performing events based on RSVPs
        private List<EventViewModel> GetTopPerformingEvents(List<EventViewModel> events)
        {
            return events
                .OrderByDescending(e => e.EventRSVPs)
                .Take(5) // Top 5 events with highest RSVPs
                .ToList();
        }

        // NEW: Fetch user growth data (new users per month)
         private async Task<List<UserGrowthModel>> GetUserGrowthData()
        {
            var users = await _eventsDatabase.GetAllUsersAsync();
            
            return users
                .GroupBy(u => u.CreatedAt.HasValue 
                    ? u.CreatedAt.Value.ToDateTime().ToString("yyyy-MM") 
                    : DateTime.UtcNow.ToString("yyyy-MM")) // Handle missing CreatedAt
                .Select(g => new UserGrowthModel
                {
                    Month = g.Key,
                    NewUsers = g.Count()
                })
                .OrderBy(g => g.Month) // Ensure chronological order
                .ToList();
        }

        // Fetch Visitors Attending the Festival (Upcoming RSVPs)
        private async Task<List<VisitorViewModel>> GetVisitorsAttendingFestival()
        {
            FirestoreDb db = GetFirestoreDb();
            CollectionReference rsvpCollection = db.Collection("RSVPsCollection");
            QuerySnapshot snapshot = await rsvpCollection.GetSnapshotAsync();

            var visitors = snapshot.Documents
                .Where(doc => doc.ContainsField("StartDate") && 
                            doc.GetValue<Timestamp>("StartDate").ToDateTime() >= DateTime.UtcNow)
                .Select(doc => new VisitorViewModel
                {
                    Email = doc.ContainsField("Email") ? doc.GetValue<string>("Email") : "Unknown",
                    Name = doc.ContainsField("Name") ? doc.GetValue<string>("Name") : doc.GetValue<string>("Email"),
                    AttendeeCount = doc.ContainsField("AttendeeCount") ? doc.GetValue<int>("AttendeeCount") : 1,
                    EventName = doc.ContainsField("EventName") ? doc.GetValue<string>("EventName") : "Unknown Event",
                    StartDate = doc.ContainsField("StartDate") ? doc.GetValue<Timestamp>("StartDate").ToDateTime() : DateTime.UtcNow
                })
                .ToList();

            return visitors;
        }
        
        // Fetch Attendees for Past Events
        private async Task<List<AttendeeViewModel>> GetPastEventAttendees()
        {
            FirestoreDb db = GetFirestoreDb();
            CollectionReference rsvpCollection = db.Collection("RSVPsCollection");
            QuerySnapshot snapshot = await rsvpCollection.GetSnapshotAsync();

            var pastAttendees = snapshot.Documents
                .Where(doc => doc.ContainsField("StartDate") &&
                            doc.GetValue<Timestamp>("StartDate").ToDateTime() < DateTime.UtcNow)
                .Select(doc => new AttendeeViewModel
                {
                    Email = doc.ContainsField("Email") ? doc.GetValue<string>("Email") : "Unknown",
                    Name = doc.ContainsField("Name") ? doc.GetValue<string>("Name") : doc.GetValue<string>("Email"),
                    AttendeeCount = doc.ContainsField("AttendeeCount") ? doc.GetValue<int>("AttendeeCount") : 1,
                    EventName = doc.ContainsField("EventName") ? doc.GetValue<string>("EventName") : "Unknown Event",
                    StartDate = doc.ContainsField("StartDate") ? doc.GetValue<Timestamp>("StartDate").ToDateTime() : DateTime.UtcNow
                })
                .ToList();

            return pastAttendees;
        }

        
    }
}