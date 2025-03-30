using Microsoft.AspNetCore.Mvc;

namespace EtechTaskManagerBackend.DTO
{
    public class TasksDTO
    {
          public int Id { get; set; }  // Task ID
        public string Title { get; set; }  // Task title
        public string Description { get; set; }  // Task description
        public int? AssignedTo { get; set; }  // User ID the task is assigned to
        public string Status { get; set; }  // Task status (e.g., pending, completed)
        public DateTime CreatedAt { get; set; } = DateTime.Now;  // Task creation timestamp
        public DateTime? DueDate { get; set; } // Task due date
        public string DueDateDisplay => DueDate?.ToString("dd-MM-yyyy") ?? "Pa Deadline"; // Display logicwhe
        public string? FilePath { get; set; }
        public IFormFile? File { get; set; }
        public int CreatedBy { get; set; } // New property
    }
}
