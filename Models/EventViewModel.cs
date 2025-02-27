using System;
using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations.Schema; // ✅ Required for NotMapped
using System.Text.Json.Serialization;

namespace Cape_Town_Festival.Models
{
    [FirestoreData]
    public class EventViewModel
    {
        [FirestoreProperty]
        public int? EventID { get; set; }

        [FirestoreProperty]
        public string EventName { get; set; } = string.Empty;

        [FirestoreProperty]
        public string EventCategory { get; set; } = string.Empty;

        [FirestoreProperty]
        public string EventDescription { get; set; } = string.Empty;

        [FirestoreProperty]
        public string EventImageURL { get; set; } = string.Empty;

        [FirestoreProperty]
        public GeoPoint EventLatLong { get; set; } = new GeoPoint(0.0, 0.0);

        [NotMapped]
        [JsonIgnore]
        public string Latitude
        {
            get => EventLatLong.Latitude.ToString();
            set
            {
                if (double.TryParse(value, out double lat))
                {
                    EventLatLong = new GeoPoint(lat, EventLatLong.Longitude);
                }
            }
        }

        [NotMapped]
        [JsonIgnore]
        public string Longitude
        {
            get => EventLatLong.Longitude.ToString();
            set
            {
                if (double.TryParse(value, out double lon))
                {
                    EventLatLong = new GeoPoint(EventLatLong.Latitude, lon);
                }
            }
        }

        [FirestoreProperty]
        public string EventLocation { get; set; } = string.Empty;

        [FirestoreProperty]
        public int EventMaxAttendees { get; set; } = 0;

        [FirestoreProperty]
        public double EventRatings { get; set; } = 0.0;

        [FirestoreProperty]
        public Timestamp EventDate { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);

        [FirestoreProperty]
        public string Status { get; set; } = "Online";

        [FirestoreProperty]
        public int EventRSVPs { get; set; } = 0;

        [FirestoreProperty]
        public int AttendeeCount { get; set; } = 0;

        public int SeatsLeft => Math.Max(0, EventMaxAttendees - EventRSVPs);

        //  Fix: Convert Firestore Timestamp correctly for HTML input format
        [NotMapped]
        [JsonIgnore]
        public string EventDateFormatted
        {
            get => GetEventDateAsDateTime().ToString("yyyy-MM-ddTHH:mm");
            set
            {
                if (DateTime.TryParse(value, out DateTime parsedDate))
                {
                    EventDate = Timestamp.FromDateTime(parsedDate.ToUniversalTime());
                }
            }
        }

        // Converts Firestore Timestamp to Local DateTime
        public DateTime GetEventDateAsDateTime()
        {
            return EventDate.ToDateTime().ToLocalTime(); // Converts UTC to Local Time
        }

        public string GetFormattedLocation()
        {
            return $"{EventLatLong.Latitude}, {EventLatLong.Longitude}";
        }
    }
}