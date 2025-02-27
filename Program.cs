using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Cape_Town_Festival.Database;
using Cape_Town_Festival.Services;

var builder = WebApplication.CreateBuilder(args);

// Enable Logging
builder.Services.AddLogging();

// Firebase Setup
var credentialPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "firebase-adminsdk.json");
var credential = GoogleCredential.FromFile(credentialPath);
FirebaseApp.Create(new AppOptions
{
    Credential = credential,
    ProjectId = "cpt-festival"
});

// Firestore Setup
var firestoreDb = new FirestoreDbBuilder
{
    ProjectId = "cpt-festival",
    Credential = credential
}.Build();

// Register FirestoreDb as a Singleton
builder.Services.AddSingleton(firestoreDb);

//weather API
builder.Services.AddScoped<WeatherService>();

// Register EventsDatabase with Logging
builder.Services.AddSingleton<EventsDatabase>(provider =>
{
    var db = provider.GetRequiredService<FirestoreDb>();
    var logger = provider.GetRequiredService<ILogger<EventsDatabase>>();
    return new EventsDatabase(db, logger);
});

// Basic Services
builder.Services.AddControllersWithViews();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/LogIn";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Configure Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Events Routes
app.MapControllerRoute(
    name: "events",
    pattern: "Events/{action=Index}/{id?}",
    defaults: new { controller = "Events" }
);

// RSVP Routes
app.MapControllerRoute(
    name: "rsvp",
    pattern: "RSVP/{action=Form}/{id?}",
    defaults: new { controller = "RSVP" }
);

// Dynamic Events Route
app.MapControllerRoute(
    name: "eventsDynamic",
    pattern: "Events/Dynamic",
    defaults: new { controller = "Events", action = "EventsDynamic" }
);

// Learn More Route for Events
app.MapControllerRoute(
    name: "eventDetails",
    pattern: "Events/LearnMore/{eventId?}",
    defaults: new { controller = "Events", action = "LearnMore" }
);

app.Run();