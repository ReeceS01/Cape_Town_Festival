using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class ReviewsController : Controller
{
    private readonly FirestoreDb _firestoreDb;

    public ReviewsController()
    {
        string credentialPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "firebase-adminsdk.json");
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
        _firestoreDb = FirestoreDb.Create("cpt-festival"); // Ensure this matches your Firestore Project ID
    }

    public async Task<IActionResult> Reviews()
    {
        try
        {
            var reviewsData = new Dictionary<string, Dictionary<string, List<Dictionary<string, object>>>>();

            // Fetch RSVP records that have UserReview
            Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection")
                .WhereNotEqualTo("UserReview", null); // ✅ Keep only one filter

            QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

            foreach (DocumentSnapshot rsvpDoc in rsvpSnapshot.Documents)
            {
                var rsvpData = rsvpDoc.ToDictionary();

                if (!rsvpData.ContainsKey("UserReview") || string.IsNullOrWhiteSpace(rsvpData["UserReview"].ToString()))
                    continue; //  Skip empty reviews (instead of using .WhereNotEqualTo)
                            string eventId = rsvpData["EventID"].ToString();
                            string reviewText = rsvpData["UserReview"].ToString();
                            double rating = Convert.ToDouble(rsvpData["UserRating"]);
                            string reviewDate = rsvpData.ContainsKey("ReviewDate") && rsvpData["ReviewDate"] is Timestamp reviewTimestamp
                                                ? reviewTimestamp.ToDateTime().ToString("yyyy-MM-dd") : "Unknown Date";
                            string userFirstName = rsvpData.ContainsKey("UserFirstName") ? rsvpData["UserFirstName"].ToString() : "Anonymous";

                            //  Fetch Event details from EventsCollection
                            DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId);
                            DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

                            string eventName = "Unknown Event";
                            string eventCategory = "Miscellaneous"; // Default category

                        if (eventSnapshot.Exists)
                        {
                            var eventData = eventSnapshot.ToDictionary();
                            eventName = eventData.ContainsKey("EventName") ? eventData["EventName"].ToString() : "Unknown Event";
                            eventCategory = eventData.ContainsKey("EventCategory") ? eventData["EventCategory"].ToString() : "Miscellaneous";
                        }

                    //  Ensure category exists
                    if (!reviewsData.ContainsKey(eventCategory))
                    {
                        reviewsData[eventCategory] = new Dictionary<string, List<Dictionary<string, object>>>();
                    }

                //  Ensure event exists under category
                if (!reviewsData[eventCategory].ContainsKey(eventName))
                {
                    reviewsData[eventCategory][eventName] = new List<Dictionary<string, object>>();
                }

            //  Add review entry
            reviewsData[eventCategory][eventName].Add(new Dictionary<string, object>
            {
                 { "UserFirstName", userFirstName ?? "Anonymous" }, 
                { "UserReview", reviewText ?? "No review provided" },
                { "UserRating", rating },
                { "ReviewDate", reviewDate }
            });
        }

            ViewBag.Reviews = reviewsData;
        }
        catch (Exception ex)
        {
            ViewBag.Error = "Error fetching reviews: " + ex.Message;
        }

        return View();
    }
}