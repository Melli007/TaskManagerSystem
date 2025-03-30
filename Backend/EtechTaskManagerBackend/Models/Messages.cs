using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EtechTaskManagerBackend.Models
{
    public class Messages
    {
        [Key]
        public int MID { get; set; } // Primary Key for the message

        [Required]
        [ForeignKey("Sender")]
        public int SenderId { get; set; } // Foreign Key linking to the Users table (Sender)

        [Required]
        [ForeignKey("Recipient")]
        public int RecipientId { get; set; } // Foreign Key linking to the Users table (Recipient)

        public string? Message { get; set; } // The content of the message

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow; // The timestamp when the message is sent

        public bool IsRead { get; set; } = false; // Whether the message has been read (default: false)

        public DateTime? ReadAt { get; set; } // Nullable, indicates when the message was read

        public string? FilePath { get; set; }
        public bool IsEdited { get; set; } = false;   // default to false

        public bool IsDeletedForEveryone { get; set; } = false;
        public bool IsVisibleToSender { get; set; } = true;
        public bool IsVisibleToRecipient { get; set; } = true;
        // Navigation properties
        public Users Sender { get; set; } // Navigation property for the Sender
        public Users Recipient { get; set; } // Navigation property for the Recipient
    }
}
