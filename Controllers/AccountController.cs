using Cape_Town_Festival.Database;
using Cape_Town_Festival.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using iTextSharp.text;

namespace Cape_Town_Festival.Controllers
{
    public class AccountController : Controller
    {
        private readonly FirestoreDb _firestoreDb;

        public AccountController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        // GET: Sign Up Page
        [HttpGet]
        public IActionResult SignUp()
        {
            return View("SignUp");
        }

        // POST: Handle Sign Up
        [HttpPost]
        public async Task<IActionResult> SignUp(string email, string password, string firstName, string lastName, string gender, DateTime birthdate)
        {
            try
            {
                var firebaseAuth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                var userRecord = await firebaseAuth.CreateUserAsync(new UserRecordArgs
                {
                    Email = email,
                    Password = password,
                    DisplayName = $"{firstName} {lastName}"
                });

                DocumentReference docRef = _firestoreDb.Collection("UserCollection").Document(email);
                await docRef.SetAsync(new
                {
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Gender = gender,
                    Birthdate = Timestamp.FromDateTime(birthdate.ToUniversalTime()),
                    Role = "Visitor",
                    CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
                });

                // Set claims for the new user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, email),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                    new Claim(ClaimTypes.Role, "Visitor")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity)
                );

                TempData["Message"] = "Sign-up successful! Redirecting to your Account Home...";
                return RedirectToAction("AccountHome");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sign-up error: {ex.Message}");
                ViewBag.Error = "Error during sign-up: " + ex.Message;
                return View("SignUp");
            }
        }   

        // GET: Log In Page
        [HttpGet]
        public IActionResult LogIn()
        {
            return View("LogIn");
        }

        // POST: Handle Log In
      [HttpPost]
        public async Task<IActionResult> LogIn(string email, string password)
        {
            try
            {
                var firebaseAuth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                var user = await firebaseAuth.GetUserByEmailAsync(email);

                if (user == null)
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View("LogIn");
                }

                DocumentReference docRef = _firestoreDb.Collection("UserCollection").Document(email);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    ViewBag.Error = "User data not found.";
                    return View("LogIn");
                }

                var userData = snapshot.ToDictionary();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, email),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Name, $"{userData["FirstName"]} {userData["LastName"]}"),
                    new Claim(ClaimTypes.Role, userData["Role"].ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity)
                );

                TempData["Message"] = "Log-in successful! Redirecting to your Account Home...";
                return RedirectToAction("AccountHome");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log-in error: {ex.Message}");
                ViewBag.Error = "Error during log-in: " + ex.Message;
                return View("LogIn");
            }
        }

     [HttpGet]
public async Task<IActionResult> AccountHome()
{
    try
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            ViewBag.Error = "Error: User is not authenticated.";
            return View("AccountHome");
        }

        // Fetch user data
        DocumentReference userDocRef = _firestoreDb.Collection("UserCollection").Document(email);
        DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

        if (userSnapshot.Exists)
        {
            var userData = userSnapshot.ToDictionary();
            ViewBag.UserData = userData;
        }
        else
        {
            ViewBag.Error = "Error: User data is not available.";
            return View("AccountHome");
        }

        // Fetch RSVP data for the user
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection").WhereEqualTo("Email", email);
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        var rsvpList = new List<Dictionary<string, object>>();
        foreach (DocumentSnapshot rsvpDoc in rsvpSnapshot.Documents)
        {
            var rsvpData = rsvpDoc.ToDictionary();

            // Restore previous approach: Use RSVP StartDate
            if (rsvpData.ContainsKey("StartDate") && rsvpData["StartDate"] is Google.Cloud.Firestore.Timestamp rsvpTimestamp)
            {
                rsvpData["StartDate"] = rsvpTimestamp.ToDateTime(); //  Convert to DateTime
            }
            else
            {
                rsvpData["StartDate"] = "Date not available";
            }

            rsvpList.Add(rsvpData);
        }

        ViewBag.RSVPs = rsvpList;
        return View("AccountHome");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AccountHome error: {ex.Message}");
        ViewBag.Error = "Error loading account home: " + ex.Message;
        return View("AccountHome");
    }
}

        
        // POST: Log Out
        [HttpPost]
        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
        
       [HttpGet]
        public async Task<IActionResult> EditAccount()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "You must be logged in to edit your account.";
                return RedirectToAction("LogIn", "Account");
            }

            DocumentReference docRef = _firestoreDb.Collection("UserCollection").Document(email);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists || snapshot == null)
            {
                TempData["ErrorMessage"] = "User data not found.";
                return RedirectToAction("AccountHome");
            }

            var userData = snapshot.ToDictionary();
            
            // Ensure userData is not null before using it
            if (userData == null)
            {
                TempData["ErrorMessage"] = "User data could not be loaded.";
                return RedirectToAction("AccountHome");
            }

            ViewBag.FirstName = userData.ContainsKey("FirstName") ? userData["FirstName"].ToString() : "";
            ViewBag.LastName = userData.ContainsKey("LastName") ? userData["LastName"].ToString() : "";
            ViewBag.Gender = userData.ContainsKey("Gender") ? userData["Gender"].ToString() : "";

            return View();
        }
        
        [HttpPost] //Edit account means visitor can change their name, surname and gender
        public async Task<IActionResult> EditAccount(string firstName, string lastName, string gender)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    TempData["ErrorMessage"] = "You must be logged in to edit your account.";
                    return RedirectToAction("LogIn", "Account");
                }

                // Reference to the user document in Firestore
                DocumentReference docRef = _firestoreDb.Collection("UserCollection").Document(email);
                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "FirstName", firstName },
                    { "LastName", lastName },
                    { "Gender", gender }
                };

                await docRef.UpdateAsync(updates);

                TempData["SuccessMessage"] = "Your account details have been updated successfully!";
                
                //  Redirect to AccountHome.cshtml after successful update
                return RedirectToAction("AccountHome");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating account details: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating your account. Please try again.";
                
                // Redirect back to EditAccount.cshtml if there's an error
                return RedirectToAction("EditAccount");
            }
        } 
        
        //Saving Editings (saving the account)
        [HttpPost]
        public async Task<IActionResult> SaveAccountChanges(string firstName, string lastName, string gender)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("AccountHome");
            }

            try
            {
                DocumentReference userRef = _firestoreDb.Collection("UserCollection").Document(email);

                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "FirstName", firstName },
                    { "LastName", lastName },
                    { "Gender", gender } // Save Gender
                };

                await userRef.UpdateAsync(updates);

                TempData["SuccessMessage"] = "Your account details have been updated!";
                return RedirectToAction("AccountHome");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating account: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating your account.";
                return RedirectToAction("EditAccount");
            }
        } 
          
        [HttpPost]
        public async Task<IActionResult> Edit(int eventId, int newAttendeeCount)
        {
            try
            {
                // Fetch the logged-in user's email
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    TempData["ErrorMessage"] = "You must be logged in to edit your RSVP.";
                    return RedirectToAction("LogIn", "Account");
                }

                // Update only the AttendeeCount in Firestore
                var rsvpDb = new RSVPDatabase(_firestoreDb);
                var success = await rsvpDb.UpdateRSVP(email, eventId, newAttendeeCount);

                if (success)
                {
                    TempData["SuccessMessage"] = "Your RSVP has been successfully updated!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update your RSVP. Please try again.";
                }

                return RedirectToAction("AccountHome", "Account");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error updating RSVP: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating your RSVP. Please try again.";
                return RedirectToAction("AccountHome", "Account");
            }
        }

        [HttpGet]
public async Task<IActionResult> LeaveReview(int eventId)
{
    try
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "You must be logged in to leave a review.";
            return RedirectToAction("AccountHome");
        }

        // Fetch RSVP entry for this user and event
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection")
            .WhereEqualTo("Email", email)
            .WhereEqualTo("EventID", eventId);
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        if (rsvpSnapshot.Documents.Count == 0)
        {
            TempData["ErrorMessage"] = "You did not RSVP for this event.";
            return RedirectToAction("AccountHome");
        }

        var rsvpData = rsvpSnapshot.Documents[0].ToDictionary();

        // Ensure the event details are available
        DocumentReference eventRef = _firestoreDb.Collection("EventsCollection").Document(eventId.ToString());
        DocumentSnapshot eventSnapshot = await eventRef.GetSnapshotAsync();

        if (!eventSnapshot.Exists)
        {
            TempData["ErrorMessage"] = "Event details not found.";
            return RedirectToAction("AccountHome");
        }

        var eventData = eventSnapshot.ToDictionary();

        // Create ViewModel to pass to the view
        var model = new PreviousEventViewModel
        {
            EventID = eventId,
            EventName = rsvpData.ContainsKey("EventName") ? rsvpData["EventName"].ToString() : "Unknown Event",
            StartDate = eventData.ContainsKey("EventDate") && eventData["EventDate"] is Google.Cloud.Firestore.Timestamp eventTimestamp
                        ? eventTimestamp.ToDateTime() : DateTime.MinValue,
            Location = rsvpData.ContainsKey("Location") ? rsvpData["Location"].ToString() : "Unknown",
            UserRating = rsvpData.ContainsKey("UserRating") ? Convert.ToDouble(rsvpData["UserRating"]) : (double?)null,
            UserReview = rsvpData.ContainsKey("UserReview") ? rsvpData["UserReview"].ToString() : null
        };

        return View("~/Views/Reviews/LeaveReview.cshtml", model);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"LeaveReview error: {ex.Message}");
        TempData["ErrorMessage"] = "An error occurred while loading the review page.";
        return RedirectToAction("AccountHome");
    }
}

   [HttpPost]
public async Task<IActionResult> SubmitReview(int eventId, double userRating, string userReview)
{
    try
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value; //  Fetch First Name from Claims

        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "You must be logged in to submit a review.";
            return RedirectToAction("AccountHome");
        }

        // 🛠 If first name is missing, fetch from Firestore
        if (string.IsNullOrEmpty(firstName))
        {
            DocumentReference userRef = _firestoreDb.Collection("UserCollection").Document(email);
            DocumentSnapshot userSnapshot = await userRef.GetSnapshotAsync();
            
            if (userSnapshot.Exists && userSnapshot.ContainsField("FirstName"))
            {
                firstName = userSnapshot.GetValue<string>("FirstName");
            }
        }

        //  Default to "Anonymous" if still missing
        firstName = firstName ?? "Anonymous";

        if (userRating < 0 || userRating > 5 || string.IsNullOrWhiteSpace(userReview))
        {
            TempData["ErrorMessage"] = "Invalid rating or review content.";
            return RedirectToAction("LeaveReview", new { eventId });
        }

        // Fetch RSVP document for the user and event
        Query rsvpQuery = _firestoreDb.Collection("RSVPsCollection")
            .WhereEqualTo("Email", email)
            .WhereEqualTo("EventID", eventId);
        QuerySnapshot rsvpSnapshot = await rsvpQuery.GetSnapshotAsync();

        if (rsvpSnapshot.Documents.Count == 0)
        {
            TempData["ErrorMessage"] = "You did not RSVP for this event.";
            return RedirectToAction("AccountHome");
        }

        DocumentReference rsvpDocRef = rsvpSnapshot.Documents[0].Reference;

        //  Save First Name, Review Date, Rating, and Review
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "UserRating", userRating },
            { "UserReview", userReview },
            { "ReviewDate", Timestamp.FromDateTime(DateTime.UtcNow) }, // Save Date
            { "UserFirstName", firstName } //  Save First Name from Firestore
        };

        await rsvpDocRef.UpdateAsync(updates);

        TempData["SuccessMessage"] = "Thank you for your review!";
        return RedirectToAction("AccountHome");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"SubmitReview error: {ex.Message}");
        TempData["ErrorMessage"] = "An error occurred while submitting the review.";
        return RedirectToAction("AccountHome");
    }
}
    }
}
