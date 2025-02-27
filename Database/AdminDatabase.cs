using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cape_Town_Festival.Database
{
    public class AdminDatabase
    {
        private readonly FirestoreDb _firestoreDb;

        public AdminDatabase(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        // Add New Admin (WITHOUT Hashing)
        public async Task<bool> AddAdminAsync(string email, string password)
        {
            try
            {
                DocumentReference adminRef = _firestoreDb.Collection("AdminCollection").Document(email);
                Dictionary<string, object> adminData = new Dictionary<string, object>
                {
                    { "Email", email },
                    { "Password", password }, // ⚠️ NOT HASHED - (Change this in the future)
                    { "Role", "Admin" },
                    { "CreatedAt", Timestamp.GetCurrentTimestamp() }
                };

                await adminRef.SetAsync(adminData);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error adding admin: {ex.Message}");
                return false;
            }
        }

        //  Authenticate Admin (No Hashing)
        public async Task<bool> AuthenticateAdminAsync(string email, string password)
        {
            try
            {
                DocumentReference adminRef = _firestoreDb.Collection("AdminCollection").Document(email);
                DocumentSnapshot snapshot = await adminRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    Console.WriteLine("❌ Admin not found in Firestore.");
                    return false;
                }

                // Get stored password (not hashed)
                string storedPassword = snapshot.GetValue<string>("Password");

                Console.WriteLine($"🔍 Stored Password: {storedPassword}");
                Console.WriteLine($"🔍 Entered Password: {password}");

                bool isValid = storedPassword == password;
                Console.WriteLine($"🔑 Password Match: {isValid}");

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error authenticating admin: {ex.Message}");
                return false;
            }
        }

        //  Fetch All Admins (For Display on Admin Panel)
        public async Task<List<Dictionary<string, object>>> GetAllAdminsAsync()
        {
            try
            {
                QuerySnapshot querySnapshot = await _firestoreDb.Collection("AdminCollection").GetSnapshotAsync();
                List<Dictionary<string, object>> admins = new List<Dictionary<string, object>>();

                foreach (DocumentSnapshot doc in querySnapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        admins.Add(doc.ToDictionary());
                    }
                }

                Console.WriteLine($" Fetched {admins.Count} admins.");
                return admins;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching admins: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        //  Delete Admin
        public async Task<bool> DeleteAdminAsync(string email)
        {
            try
            {
                DocumentReference adminRef = _firestoreDb.Collection("AdminCollection").Document(email);
                await adminRef.DeleteAsync();

                Console.WriteLine($"✅ Admin {email} deleted successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting admin: {ex.Message}");
                return false;
            }
        }
    }
}