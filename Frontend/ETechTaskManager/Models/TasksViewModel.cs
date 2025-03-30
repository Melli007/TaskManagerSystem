using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ETechTaskManager.Models
{
    public class TasksViewModel
    {
        public int Id { get; set; }  // Task ID
        public string Title { get; set; }  // Task title
        public string Description { get; set; }  // Task description
        public int? AssignedTo { get; set; }  // User ID the task is assigned to

        public string Status { get; set; }  // Task status (e.g., pending, completed)
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Default to current datetime
        public DateTime? DueDate { get; set; } // Task due date
        public string DueDateDisplay => DueDate?.ToString("dd-MM-yyyy") ?? "Pa Deadline"; // Display logicwhe

        public IFormFile File { get; set; } // Used to upload file
        public string? FilePath { get; set; } // Store the uploaded file path after upload

        public List<UsersViewModel> Users { get; set; }  // List of users for dropdown
        public string? AssignedToName { get; set; } // Add this line

        // New properties
        public int CreatedBy { get; set; } // User ID who created the task
        public string? CreatedByName { get; set; } // Display name of the creator
    }
}
