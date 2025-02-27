using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cape_Town_Festival.Models;

namespace Cape_Town_Festival.Database
{
    public class RSVPDatabase
    {
        private readonly FirestoreDb _firestoreDb;

        public RSVPDatabase(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        // Check if User has Already RSVP’d
        public async Task<bool> UserHasRSVPd(string email, int eventId)
        {
            try
            {
                var documentId = $"{email}_{eventId}";
                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);
                DocumentSnapshot snapshot = await rsvpRef.GetSnapshotAsync();
                return snapshot.Exists;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking RSVP existence: {ex.Message}");
                return false;
            }
        }

        // Save New RSVP Entry
        public async Task<bool> SaveRSVPAsync(RSVP rsvp)
        {
            try
            {
                if (await UserHasRSVPd(rsvp.Email, rsvp.EventID))
                    return false; // User already RSVP’d

                var documentId = $"{rsvp.Email}_{rsvp.EventID}";
                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);

                //  Fetch Start Date from EventsCollection
                DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(rsvp.EventID.ToString());
                DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

                //  Ensure eventStartDate is initialized properly
                Timestamp eventStartDate = Timestamp.GetCurrentTimestamp();

                if (eventSnapshot.Exists && eventSnapshot.ContainsField("EventDate"))
                {
                    eventStartDate = eventSnapshot.GetValue<Timestamp>("EventDate");
                }

                Dictionary<string, object> rsvpData = new Dictionary<string, object>
                {
                    { "Email", rsvp.Email },
                    { "EventID", rsvp.EventID },
                    { "EventName", rsvp.EventName },
                    { "Category", rsvp.Category },
                    { "StartDate", eventStartDate }, //  Corrected
                    { "AttendeeCount", rsvp.AttendeeCount },
                    { "Location", rsvp.Location },
                    { "RSVPDate", Timestamp.GetCurrentTimestamp() }
                };

                await rsvpRef.SetAsync(rsvpData);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving RSVP: {ex.Message}");
                return false;
            }
        }

        // Edit Existing RSVP
        public async Task<bool> UpdateRSVPAsync(string email, int eventId, int newAttendeeCount)
        {
            try
            {
                if (!await UserHasRSVPd(email, eventId))
                    return false; // Cannot update a non-existent RSVP

                var documentId = $"{email}_{eventId}";
                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);
                await rsvpRef.UpdateAsync("AttendeeCount", newAttendeeCount);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating RSVP: {ex.Message}");
                return false;
            }
        }

        // Cancel RSVP
        public async Task<bool> CancelRSVPAsync(string email, string eventId)
        {
            try
            {
                var documentId = $"{email}_{eventId}";
                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(documentId);

                DocumentSnapshot snapshot = await rsvpRef.GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                    Console.WriteLine("RSVP not found in database.");
                    return false; // RSVP doesn't exist
                }

                await rsvpRef.DeleteAsync();
                Console.WriteLine("RSVP successfully deleted.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error canceling RSVP: {ex.Message}");
                return false;
            }
        }   

        //edit (Update) RSVP 
        public async Task<bool> UpdateRSVP(string email, int eventId, int newAttendeeCount)
        {
            try
            {
                if (!await UserHasRSVPd(email, eventId))
                    return false; // Cannot update a non-existent RSVP

                DocumentReference rsvpRef = _firestoreDb.Collection("RSVPsCollection").Document(email + "_" + eventId);
                
                //  Only update AttendeeCount
                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "AttendeeCount", newAttendeeCount }
                };

                await rsvpRef.UpdateAsync(updates);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating RSVP: {ex.Message}");
                return false;
            }
        }

    }
}