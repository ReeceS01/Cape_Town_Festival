namespace Cape_Town_Festival.Models
{
    public class AttendeeViewModel
    {
        public string Email { get; set; } = string.Empty;  //  Fix non-nullable issue
        public string Name { get; set; } = string.Empty;
        public int AttendeeCount { get; set; } = 0;
        public string EventName { get; set; } = string.Empty; //  Ensure EventName exists
        public DateTime StartDate { get; set; } = DateTime.MinValue; // Ensure StartDate exists
    }
}