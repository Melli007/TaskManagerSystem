using EtechTaskManagerBackend.DTO;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ETechTaskManager.Models
{
    public class UsersViewModel
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }
        public string? Profession { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsOnline { get; set; } // or a custom property
        public List<TasksDTO>? Tasks { get; set; }
        public bool Banned { get; set; } // Add this property
        public string? ProfilePicturePath { get; set; } // Add this property
        public double PerformancePercentage { get; set; } // New field
        public int OverdueTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }

        public int TotalTasks { get; set; } // ✅ Add this property
    }
}
