using System.Collections.Generic;

namespace Cape_Town_Festival.Models
{
    public class DashboardViewModel
    {
        public int TotalEvents { get; set; }
        public int TotalUsers { get; set; }
        public double AverageOccupancyRate { get; set; }
        public double AverageEventRating { get; set; }
        public List<EventCategory> PopularCategories { get; set; } = new();
        public List<EventViewModel> UpcomingEvents { get; set; } = new();
        public List<User> RecentUsers { get; set; } = new();
        public List<EventViewModel> TopPerformingEvents { get; set; } = new();
        public List<UserGrowthModel> UserGrowth { get; set; } = new();

        // ✅ Uses new separate models
        public List<VisitorViewModel> VisitorsAttendingFestival { get; set; } = new();
        public List<AttendeeViewModel> PastEventAttendees { get; set; } = new();
    }

    // ✅ Create a model to store user growth data
    public class UserGrowthModel
    {
        public string Month { get; set; }  // Example: "January"
        public int NewUsers { get; set; }  // Example: 150 new users
    }


    
}

