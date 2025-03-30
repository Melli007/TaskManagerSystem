namespace EtechTaskManagerBackend.DTO
{
    public class MessagesDTO
    {
        public int MID { get; set; } // Primary Key for the message
        public int SenderId { get; set; } // Foreign Key linking to the Users table (Sender)
        public int RecipientId { get; set; } // Foreign Key linking to the Users table (Recipient)
        public string? Message { get; set; } // The content of the message
        public string? FilePath { get; set; } // Add this line
       // The file you want to upload
        public IFormFile? File { get; set; }
        public bool IsRead { get; set; } = false; // Whether the message has been read (default: false)
        public bool IsEdited { get; set; } = false;   // default to false
        public bool IsDeletedForEveryone { get; set; } = false;
        public bool IsVisibleToSender { get; set; } = true;
        public bool IsVisibleToRecipient { get; set; } = true;
    }
}
