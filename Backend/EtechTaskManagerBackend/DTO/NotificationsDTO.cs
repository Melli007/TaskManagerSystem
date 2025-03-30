using System;

namespace EtechTaskManagerBackend.DTO
{
    public class NotificationsDTO
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public int? Recipient { get; set; } // This should link to the User ID
        public string Type { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        public string? CreatedBy { get; set; }
    }
}
