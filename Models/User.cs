using Google.Cloud.Firestore;

namespace Cape_Town_Festival.Models
{
    [FirestoreData]
    public class User
    {
        [FirestoreProperty]
        public string? FirstName { get; set; }

        [FirestoreProperty]
        public string? LastName { get; set; }

        [FirestoreProperty]
        public string? Gender { get; set; }

        [FirestoreProperty]
        public string? Email { get; set; }  

        [FirestoreProperty]
        public Timestamp? Birthdate { get; set; }

        [FirestoreProperty]
        public string? Role { get; set; }

        //  Add CreatedAt for tracking user creation time
        [FirestoreProperty]
        public Timestamp? CreatedAt { get; set; }

        public User()
        {
            // Default values
            Birthdate = Timestamp.FromDateTime(DateTime.UtcNow);
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow); // Default CreatedAt
        }
    }
}