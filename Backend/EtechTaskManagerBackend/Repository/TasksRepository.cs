using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace EtechTaskManagerBackend.Repository
{
    public class TasksRepository : ITasksRepository
    {
        private readonly DataContext _context;

        public TasksRepository(DataContext context)
        {
               _context = context;
        }

        public ICollection<Tasks> GetAllTasks()
        {
            return _context.Tasks.OrderBy(t => t.Id).ToList();
        }

        public Tasks GetTask(int id)
        {
            return _context.Tasks.Where(t => t.Id == id).FirstOrDefault();
        }

      public bool CreateTask(Tasks task)
      {
         _context.Add(task);
         return Save();
      }


        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool TaskExists(int id)
        {
            return _context.Tasks.Any(t => t.Id == id);
        }

        public bool AddTask(Tasks task)
        {
            // Add the task to the database
            _context.Tasks.Add(task);

            if (task.AssignedTo != null)
            {
                // Create a notification for the user
                var notification = new Notifications
                {
                    Message = $"Detyrë e re ju është caktuar: '{task.Title.Trim()}'",
                    Recipient = task.AssignedTo.Value, // Assuming AssignedTo is not null
                    Type = "Task",
                    Date = DateTime.Now,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }
            return Save(); // Call Save method to persist changes
        }

        public bool UpdateTask(Tasks task)
        {
            if (task.AssignedTo == null || string.IsNullOrWhiteSpace(task.Title))
            {
                Console.WriteLine("Task does not have a valid recipient or title. Cannot update notification.");
                return false;
            }

            // Retrieve the original task from the database
            var originalTask = _context.Tasks.AsNoTracking().FirstOrDefault(t => t.Id == task.Id);
            if (originalTask == null)
            {
                Console.WriteLine($"Task with ID '{task.Id}' not found. Cannot update.");
                return false;
            }

            string originalTitle = originalTask.Title;
            string newTitle = task.Title;

            // Search for a matching notification
            var notification = _context.Notifications
                .FirstOrDefault(n =>
                    n.Recipient == task.AssignedTo &&
                    n.Type == "Task" &&
                    n.Message.Equals($"Detyrë e re ju është caktuar: '{originalTitle}'") ||
                    n.Message.Equals($"Detyra '{originalTitle}' është përditësuar.")
                );

            if (notification != null)
            {
                // Update the notification
                notification.Message = $"Detyra '{newTitle}' është përditësuar.";
                _context.Notifications.Update(notification);
                Console.WriteLine($"Notification updated for task '{newTitle}'.");
            }
            else
            {
                Console.WriteLine($"\n\n\nNotification not found for task '{originalTask}'. No updates made to notifications.\n\n\n");
            }

            _context.Tasks.Update(task);
            return Save();
        }


        public bool DeleteTask(Tasks task)
        {
            // Remove the task from the database
            _context.Tasks.Remove(task);

            // Find and delete the associated notification(s) for this task
            var notifications = _context.Notifications
        .Where(n =>
            n.Message.Contains($"Detyrë e re ju është caktuar: '{task.Title}'") || // Original notification message
            n.Message.Contains($"Detyra '{task.Title}' është përditësuar.")   // Edited notification message
        )
        .ToList();

            if (notifications.Any())
            {
                _context.Notifications.RemoveRange(notifications);
            }

            return Save();
        }

        public ICollection<Tasks> GetTasks(int userId)
        {
            return _context.Tasks
                .Where(t => t.AssignedTo == userId)
                .OrderBy(t => t.CreatedAt)
                .ToList();
        }

        public ICollection<Tasks> GetTasksAssignedToUser(int userId)
        {
            // Tasks that have been assigned to this user (likely by an admin)
            return _context.Tasks
                .Where(t => t.AssignedTo == userId)
                .OrderBy(t => t.CreatedAt)
                .ToList();
        }

        public ICollection<Tasks> GetTasksCreatedByUser(int userId)
        {
            // Tasks that this user created themselves
            return _context.Tasks
                .Where(t => t.CreatedBy == userId)
                .OrderBy(t => t.CreatedAt)
                .ToList();
        }

    }
}
