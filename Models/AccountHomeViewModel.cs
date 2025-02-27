using System;
using System.Collections.Generic;

namespace Cape_Town_Festival.Models
{
    public class AccountHomeViewModel
    {
        public Dictionary<string, object> UserData { get; set; }
        public List<PreviousEventViewModel> PreviousEvents { get; set; } = new List<PreviousEventViewModel>();
    }

    public class PreviousEventViewModel
    {
        public string EventName { get; set; }
        public DateTime StartDate { get; set; }
        public string Location { get; set; }
        public double? UserRating { get; set; } // Nullable to check if a review exists
        public string UserReview { get; set; }
        public int EventID { get; set; }
    }
}