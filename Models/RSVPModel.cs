using System;

namespace Cape_Town_Festival.Models
{
    public class RSVP
    {
        public string Email { get; set; } = string.Empty;
        public int EventID { get; set; }
        public string EventName { get; set; } = string.Empty;
        public Google.Cloud.Firestore.Timestamp StartDate { get; set; }  
        public string Category { get; set; } = string.Empty;
        public int AttendeeCount { get; set; }
       public string Location { get; set; } = string.Empty;
    }
} 