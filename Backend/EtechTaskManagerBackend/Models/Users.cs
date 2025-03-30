using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EtechTaskManagerBackend.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FullName{ get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(255)]
        public string Password { get; set; }


        [Required]
        public string Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Profession { get; set; }

        public bool Banned { get; set; } // Add this property

        public bool IsOnline { get; set; } // New property

        public string? ProfilePicturePath { get; set; } // Add this property

        // **Add these new properties** for reset PASSWORD flow:
        public string? ResetKey { get; set; }           // For storing GUID or token
        public DateTime? ResetKeyExpiry { get; set; }  // Optional expiry


        public ICollection<Tasks> Tasks { get; set; }
        public ICollection<Notifications> Notifications { get; set; } // One-to-many relationship
        public ICollection<Tasks> CreatedTasks { get; set; }

    }
}
