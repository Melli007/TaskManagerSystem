using System.ComponentModel.DataAnnotations;

namespace EtechTaskManagerBackend.Models
{
    public class Notifications
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        public int? Recipient { get; set; } // Change to nullable, This should link to the User ID

        public Users User { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public bool IsRead { get; set; } = false; // Default to false

        public string? CreatedBy { get; set; }
    }
}
