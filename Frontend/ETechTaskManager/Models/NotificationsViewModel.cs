using EtechTaskManagerBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace ETechTaskManager.Models
{
    public class NotificationsViewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public int Recipient { get; set; } // This should link to the User ID

        public UsersViewModel User { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false; // Default to false

        // Add this property for displaying the recipient's name
        public string? CreatedBy { get; set; }

        public string RecipientName { get; set; }
        public List<UsersViewModel> Users { get; set; }  // List of users for dropdown

    }
}
