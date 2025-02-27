namespace Cape_Town_Festival.Models
{
    public class VisitorViewModel
    {
        public string Email { get; set; } = string.Empty;  //  Default empty value to avoid null issues
        public string Name { get; set; } = string.Empty;   
        public int AttendeeCount { get; set; } = 0;
        public string EventName { get; set; } = string.Empty; 
        public DateTime StartDate { get; set; } = DateTime.MinValue; 
    }
}