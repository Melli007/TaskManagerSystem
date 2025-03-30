using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EtechTaskManagerBackend.Models
{
    public class Tasks
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        public string Description { get; set; }

        [ForeignKey("Users")]
        public int? AssignedTo { get; set; }

        [Required]
        [DefaultValue("pending")]
        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; } =  DateTime.UtcNow;
        public DateTime? DueDate { get; set; } = DateTime.UtcNow; // Task due date
        public Users User { get; set; }
        public string? FilePath { get; set; } // Path to the uploaded file

        // New field:
        [ForeignKey("Users")]
        public int CreatedBy { get; set; }
        public Users CreatedByUser { get; set; } // Navigation property (optional)

    }

}
