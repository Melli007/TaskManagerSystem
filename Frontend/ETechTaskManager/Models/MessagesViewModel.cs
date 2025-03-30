namespace EtechTaskManager.Models
{
    public class MessagesViewModel
    {
        public int MID { get; set; } // Primary Key for the message

        public int SenderId { get; set; } // Foreign Key linking to the Users table (Sender)

        public int RecipientId { get; set; } // Foreign Key linking to the Users table (Recipient)

        public string? Message { get; set; } // The content of the message

        public bool IsRead { get; set; } = false; // Whether the message has been read (default: false)

        public DateTime SentAt { get; set; } = DateTime.UtcNow; // The timestamp when the message is sent

        public DateTime? ReadAt { get; set; } // Nullable, indicates when the message was read

        public IFormFile? File { get; set; }
        public string? FilePath { get; set; }
        public bool IsEdited { get; set; } = false;   // default to false
        public bool IsDeletedForEveryone { get; set; } = false;
        public bool IsVisibleToSender { get; set; } = true;
        public bool IsVisibleToRecipient { get; set; } = true;

        // Display information for the sender and recipient
        public string SenderName { get; set; } // Full Name or Username of the sender
        public string RecipientName { get; set; } // Full Name or Username of the recipient
        public bool SenderIsOnline { get; set; }
        public bool RecipientIsOnline { get; set; }
        public int UnreadCount {  get; set; }

    }
}
