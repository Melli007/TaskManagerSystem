namespace EtechTaskManagerBackend.DTO
{
    public class UsersDTO
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string Username { get; set; }

        public string Password {  get; set; }

        public string Role { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Profession { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool Banned { get; set; } // Add this property
        public bool IsOnline { get; set; } // New property
        public string? ProfilePicturePath { get; set; } // Add this property

        // Add this to include tasks
    }
}
