using Azure.Core;
using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.EtechHubs;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using EtechTaskManagerBackend.Repository;
using EtechTaskManagerBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.VisualBasic;
using System.Text;
using System.Threading.Tasks;

namespace EtechTaskManagerBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TasksController : Controller
    {
        
        private readonly ITasksRepository _tasksRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly INotificationsRepository _notificationsRepository;
        private readonly NotificationService _notificationService;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public TasksController(ITasksRepository tasksRepository, INotificationsRepository notificationsRepository, NotificationService notificationService, IHubContext<TaskHub> hubContext, IUsersRepository usersRepository, IHubContext<NotificationHub> notificationHub)
        {
            _tasksRepository = tasksRepository;
            _notificationsRepository = notificationsRepository; // Initialize NotificationsRepository
            _notificationService = notificationService;
            _hubContext = hubContext;
            _usersRepository = usersRepository;
            _notificationHub = notificationHub;
        }

        // GET: api/tasks
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<TasksDTO>))]
        [ProducesResponseType(404)]
        public IActionResult GetAllTasks()
        {
            // Identify the Admin user. Assuming the Role == "Admin" defines the admin user.
            var adminUser = _usersRepository.GetUsers()
                .FirstOrDefault(u => u.Role != null && u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (adminUser == null)
            {
                return NotFound("Admin user not found.");
            }

            int adminUserId = adminUser.Id;

            // Get all tasks created by the Admin.
            var tasks = _tasksRepository.GetAllTasks()
                .Where(t => t.CreatedBy == adminUserId)
                .ToList();

            if (!tasks.Any())
            {
                return NotFound("No tasks found created by the Admin.");
            }

            // Map tasks to DTOs
            var tasksDTO = tasks.Select(t => new TasksDTO
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedTo = t.AssignedTo,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                FilePath = t.FilePath,
                CreatedBy = t.CreatedBy
            }).ToList();

            return Ok(tasksDTO);
        }


        //GET: api/tasks/{userId}
        [HttpGet("user/{userId}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<TasksDTO>))]
        [ProducesResponseType(404)]
        public IActionResult GetTasks(int userId)
        {
            var tasks = _tasksRepository.GetTasks(userId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tasksDTO = tasks.Select(t => new TasksDTO
            {
                Id =t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedTo = t.AssignedTo,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                FilePath = t.FilePath,
                CreatedBy = t.CreatedBy
            }
            ).ToList();

            return Ok(tasksDTO);
        }


        //GET: api/tasks/{id}
        [HttpGet ("{id}")]
        [ProducesResponseType(200, Type = typeof(Tasks))]
        [ProducesResponseType(400)]
        public IActionResult GetTask(int id) 
        {
            if (!_tasksRepository.TaskExists(id)) 
            {
                return NotFound();
            }

            var task = _tasksRepository.GetTask(id); 

            if (!ModelState.IsValid) 
            {
                return BadRequest(ModelState); 
            }

            // Mapping to DTO
            var taskDTO = new TasksDTO
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                AssignedTo = task.AssignedTo,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                DueDate = task.DueDate,
                FilePath = task.FilePath,
                CreatedBy = task.CreatedBy
            };

            return Ok(taskDTO);
        }

        [HttpGet("{profession}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<TasksDTO>))]
        [ProducesResponseType(404)]
        public IActionResult GetTasksByProfession(string profession)
        {
            // 1. Identify the Admin user. Here, we assume there is a user with Role == "Admin".
            var adminUser = _usersRepository.GetUsers()
                .FirstOrDefault(u => u.Role != null && u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (adminUser == null)
            {
                return NotFound("Admin user not found.");
            }
            int adminUserId = adminUser.Id;

            // 2. Retrieve all users with the specified Profession.
            var usersWithProfession = _usersRepository.GetUsers()
                .Where(u => u.Profession != null
                     && u.Profession.Equals(profession, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!usersWithProfession.Any())
            {
                return NotFound($"No users found with the profession '{profession}'.");
            }

            // 3. For each user in this profession, get tasks assigned to them,
            //    but *only* include tasks where CreatedBy == Admin's Id.
            var allTasks = new List<Tasks>();
            foreach (var user in usersWithProfession)
            {
                var tasksForUser = _tasksRepository.GetTasks(user.Id)
                    .Where(t => t.CreatedBy == adminUserId) // Filter only tasks created by Admin
                    .ToList();

                allTasks.AddRange(tasksForUser);
            }

            if (!allTasks.Any())
            {
                return NotFound($"No tasks found for profession '{profession}' that were created by Admin.");
            }

            // 4. Map all tasks to DTO
            var tasksDTO = allTasks.Select(t => new TasksDTO
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedTo = t.AssignedTo,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                FilePath = t.FilePath,
                CreatedBy = t.CreatedBy
            }).ToList();

            return Ok(tasksDTO);
        }


        [HttpPost]
        [Consumes("multipart/form-data")] // Expect multipart form-data
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateTask([FromForm] TasksDTO tasksCreate)
        {
            if (tasksCreate == null)
            {
                return BadRequest("Task data is required.");
            }

            // Check if the assigned user is banned
            var assignedUser = _usersRepository.GetUserById(tasksCreate.AssignedTo ?? 0);
            if (assignedUser == null || assignedUser.Banned)
            {
                return BadRequest("Cannot assign tasks to a banned user.");
            }

            // Handle file if provided
            string? filePath = null;
            if (tasksCreate.File != null && tasksCreate.File.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".jpg", ".png", ".xlsx" };
                var fileExtension = Path.GetExtension(tasksCreate.File.FileName).ToLower();
                var mimeType = tasksCreate.File.ContentType;

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Invalid file extension. Allowed types: PDF, DOCX, TXT, JPG, PNG, XLSX.");
                }

                // Check magic numbers for supported file types
                if (!IsValidFileContent(tasksCreate.File.OpenReadStream(), fileExtension))
                {
                    return BadRequest("Invalid file content. The uploaded file does not match the expected type.");
                }

                long maxFileSize = 5 * 1024 * 1024; // 5MB
                if (tasksCreate.File.Length > maxFileSize)
                {
                    return BadRequest("File size exceeds the maximum allowed size of 5MB.");
                }

                // Proceed with saving the file
                var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(tasksCreate.File.FileName)}{fileExtension}";
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "SecureUploads");
                Directory.CreateDirectory(uploadPath);
                filePath = Path.Combine(uploadPath, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await tasksCreate.File.CopyToAsync(stream);
                }

                tasksCreate.FilePath = filePath;
            }

            // Ensure CreatedAt has a value
            tasksCreate.CreatedAt = tasksCreate.CreatedAt == DateTime.MinValue ? DateTime.Now : tasksCreate.CreatedAt;

            // Check if a task with the same title, description, and assigned user already exists
            var existingTask = _tasksRepository.GetAllTasks()
                .FirstOrDefault(t =>
                    t.Title.Trim().ToUpper() == tasksCreate.Title.TrimEnd().ToUpper() &&
                    t.Description.Trim().ToUpper() == tasksCreate.Description.TrimEnd().ToUpper() &&
                    t.AssignedTo == tasksCreate.AssignedTo); // Include AssignedTo in the check

            if (existingTask != null)
            {
                ModelState.AddModelError("", "Task already exists for this user");
                return StatusCode(422, ModelState);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var createdByUser = _usersRepository.GetUserById(tasksCreate.CreatedBy);
            string createdByName = createdByUser != null ? createdByUser.FullName : "Unknown";
            // Map the TasksDTO to the Tasks model
            var tasksMap = new Tasks
            {
                Title = tasksCreate.Title,
                Description = tasksCreate.Description,
                AssignedTo = tasksCreate.AssignedTo,
                Status = tasksCreate.Status,
                CreatedAt = tasksCreate.CreatedAt,
                DueDate = tasksCreate.DueDate,
                FilePath = tasksCreate.FilePath,
                CreatedBy = tasksCreate.CreatedBy
            };


            // Attempt to create the task
            if (!_tasksRepository.CreateTask(tasksMap))
            {
                ModelState.AddModelError("", "Something went wrong while saving");
                return StatusCode(500, ModelState);
            }


            // Retrieve the Id of the newly created task
            int createdTaskId = tasksMap.Id; // Assuming the Id is set by the database

            await _hubContext.Clients.User((tasksMap.AssignedTo ?? 0).ToString())
           .SendAsync("ReceiveTask", createdTaskId, tasksMap.Title, tasksMap.Description, tasksMap.Status, tasksMap.CreatedAt, tasksMap.DueDate, createdByName, tasksMap.FilePath);

            //Filtrimi i taskqave ne realTime
            var userTasks = _tasksRepository.GetTasks(tasksMap.AssignedTo ?? 0);
            int totalTasks = userTasks.Count();
            int completedTasks = userTasks.Count(t => t.Status == "Të Përfunduara");
            int progressTasks = userTasks.Count(t => t.Status == "Në Progres");
            int pendingTasks = userTasks.Count(t => t.Status == "Në Pritje");
            int nodeadlineTasks = userTasks.Count(t => t.DueDate == null);

            Console.WriteLine($"{tasksMap.AssignedTo}");
            await _hubContext.Clients.User((tasksMap.AssignedTo ?? 0).ToString())
           .SendAsync("ReceiveTaskUpdate", totalTasks, completedTasks, progressTasks, pendingTasks, nodeadlineTasks);

            if (tasksMap.AssignedTo != tasksMap.CreatedBy)
            {
            
            // Create and send notification in real-time
            var notification = new Notifications
            {
                Message = $"Detyrë e re ju është caktuar: '{tasksMap.Title}'",
                Recipient = tasksMap.AssignedTo ?? 0,
                Type = "Task",
                Date = DateTime.Now,
                IsRead = false
            };

            _notificationService.LogNotificationDetails(notification);

            // Attempt to save notification
            if (!_notificationsRepository.AddNotification(notification))
            {
                ModelState.AddModelError("", "Failed to create notification");
                return StatusCode(500, ModelState);
            }

            await _notificationHub.Clients.User(notification.Recipient.ToString())
       .SendAsync("ReceiveNotification", notification.Id, notification.Recipient, notification.Message, notification.Type, notification.Date, notification.IsRead);
        }
            return Ok(new { taskId = tasksMap.Id , filepath = tasksMap.FilePath});
        }

        private bool IsValidFileContent(Stream fileStream, string fileExtension)
        {
            var pngMagicNumber = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG
            var jpgMagicNumber = new byte[] { 0xFF, 0xD8 };             // JPG
            var pdfMagicNumber = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF
            var docxMagicNumber = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // DOCX, XLSX (OpenXML format)
            var txtMagicNumber = new byte[] { }; // Text files don't have a strict magic number

            var magicNumbers = new Dictionary<string, byte[]>
    {
        { ".png", pngMagicNumber },
        { ".jpg", jpgMagicNumber },
        { ".jpeg", jpgMagicNumber },
        { ".pdf", pdfMagicNumber },
        { ".docx", docxMagicNumber }, // DOCX and XLSX share the same magic number
        { ".xlsx", docxMagicNumber },
        { ".txt", txtMagicNumber }    // TXT files are validated separately
    };

            if (magicNumbers.ContainsKey(fileExtension))
            {
                byte[] buffer = new byte[magicNumbers[fileExtension].Length];
                fileStream.Position = 0; // Reset stream position
                fileStream.Read(buffer, 0, buffer.Length);
                fileStream.Position = 0; // Reset again after reading

                // Special case for TXT files: skip magic number check
                if (fileExtension == ".txt") return true;

                return buffer.SequenceEqual(magicNumbers[fileExtension]);
            }

            return false;
        }


        [HttpGet("download/{taskId}")]
        public IActionResult DownloadFile(int taskId)
        {
            var task = _tasksRepository.GetTask(taskId);
            if (task == null || string.IsNullOrEmpty(task.FilePath))
            {
                return NotFound("File not found.");
            }

            var filePath = task.FilePath; // Use the stored FilePath directly
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found.");
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = Path.GetFileName(filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }


        // POST: api/tasks/changestatus/{id}
        [HttpPost("changestatus/{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ChangeTaskStatus(int id)
        {

            // Check if the task exists
            if (!_tasksRepository.TaskExists(id))
            {
                return NotFound();
            }

            // Get the current task
            var task = _tasksRepository.GetTask(id);

            // Check if the assigned user is banned
            var assignedUser = _usersRepository.GetUserById(task.AssignedTo ?? 0);
            if (assignedUser == null || assignedUser.Banned)
            {
                return BadRequest("Cannot change the status of a tasks to a banned user.");
            }

            // Determine the new status
            string newStatus = task.Status switch
            {
                "Në Pritje" => "Në Progres",
                "Në Progres" => "Të Përfunduara",
                _ => task.Status // Do not change if status is already "Të Përfunduar" or "Të Vonuara"
            };


            // If the status is the same as the current status, return BadRequest
            if (newStatus == task.Status)
            {
                return BadRequest("Status cannot be changed from 'Të Përfunduar' or 'Të Vonuara'.");
            }

            // Update the task status
            task.Status = newStatus;
            _tasksRepository.Save();

            Console.WriteLine($"{task.AssignedTo}");
            // Notify clients about the status change
            await _hubContext.Clients.User(task.AssignedTo?.ToString())
            .SendAsync("UpdateTaskStatus", id, newStatus);



            // Attempt to update the task
            if (!_tasksRepository.UpdateTask(task))
            {
                ModelState.AddModelError("", "Something went wrong while updating the task status");
                return StatusCode(500, ModelState);
            }

            return NoContent();
        }



        // PUT: api/tasks/{id}
        [HttpPut("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateTask(int id, [FromForm] TasksDTO updatedTasks)
        {
            if (updatedTasks == null)
            {
                return BadRequest(ModelState);
            }

            if (!_tasksRepository.TaskExists(id))
            {
                return NotFound();
            }

            // Retrieve the existing task
            var existingTask = _tasksRepository.GetTask(id);

            if (!ModelState.IsValid)
            {
                return NotFound();
            }  
            
            // Check if the assigned user is banned
            var assignedUser = _usersRepository.GetUserById(updatedTasks.AssignedTo ?? 0);
            if (assignedUser == null || assignedUser.Banned)
            {
                return BadRequest("Cannot update tasks to a banned user.");
            }

            // Handle file upload if provided
            string? filePath = existingTask.FilePath; // Retain the existing file path if no new file is uploaded
            if (updatedTasks.File != null && updatedTasks.File.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".jpg", ".png", ".xlsx" };
                var fileExtension = Path.GetExtension(updatedTasks.File.FileName).ToLower();
                var mimeType = updatedTasks.File.ContentType;

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Invalid file extension. Allowed types: PDF, DOCX, TXT, JPG, PNG, XLSX.");
                }

                // Check magic numbers for supported file types
                if (!IsValidFileContent(updatedTasks.File.OpenReadStream(), fileExtension))
                {
                    return BadRequest("Invalid file content. The uploaded file does not match the expected type.");
                }

                long maxFileSize = 5 * 1024 * 1024; // 5MB
                if (updatedTasks.File.Length > maxFileSize)
                {
                    return BadRequest("File size exceeds the maximum allowed size of 5MB.");
                }

                // Proceed with saving the file
                var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(updatedTasks.File.FileName)}{fileExtension}";
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "SecureUploads");
                Directory.CreateDirectory(uploadPath);
                filePath = Path.Combine(uploadPath, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await updatedTasks.File.CopyToAsync(stream);
                }
            }

            // Map the updated fields
            existingTask.Title = updatedTasks.Title ?? existingTask.Title;
            existingTask.Description = updatedTasks.Description ?? existingTask.Description;
            existingTask.AssignedTo = updatedTasks.AssignedTo ?? existingTask.AssignedTo;
            existingTask.Status = updatedTasks.Status ?? existingTask.Status;
            existingTask.DueDate = updatedTasks.DueDate ?? existingTask.DueDate;
            existingTask.FilePath = filePath;

            if (!_tasksRepository.UpdateTask(existingTask))
            {
                ModelState.AddModelError("", "Something went wrong while saving");
                return StatusCode(500, ModelState);
            }

            _tasksRepository.Save();

            Console.WriteLine($"{updatedTasks.AssignedTo}");
            await _hubContext.Clients.User(updatedTasks.AssignedTo?.ToString())
            .SendAsync("TaskUpdated", id, updatedTasks.Title, updatedTasks.Description, updatedTasks.Status, updatedTasks.DueDate, filePath);


            // Update notification details and notify the user
            var notification = _notificationsRepository.GetAllNotifications()
     .FirstOrDefault(n => n.Message.Contains($"You have been assigned a new task: '{existingTask.Title}'"));

            if (notification != null)
            {
                notification.Message = $"Task '{existingTask.Title}' has been updated.";
                notification.Date = DateTime.Now;

                _notificationsRepository.UpdateNotification(notification);

                await _notificationHub.Clients.User(notification.Recipient.ToString())
                    .SendAsync("NotificationUpdated", notification.Id, notification.Message, notification.Date);
            }

            return NoContent();
        }


        //DELETE: api/tasks/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            if (!_tasksRepository.TaskExists(id))
            {
                return NotFound();
            }

            var taskToDelete = _tasksRepository.GetTask(id);

            // Check if the assigned user is banned
            var assignedUser = _usersRepository.GetUserById(taskToDelete.AssignedTo ?? 0);
            if (assignedUser == null || assignedUser.Banned)
            {
                return BadRequest("Cannot delete tasks of a banned user.");
            }

            // Fetch notifications related to the task
               var notifications = _notificationsRepository.GetAllNotifications()
                .Where(n => n.Message.Contains($"You have been assigned a new task: '{taskToDelete.Title}'"))
                .ToList();

            Console.WriteLine($"Found {notifications.Count} notifications associated with the task.");

            // Notify clients about the notification deletion
            foreach (var notification in notifications)
            {
                Console.WriteLine($"Notifying deletion for Notification ID: {notification.Id}");
                await _notificationHub.Clients.User(notification.Recipient.ToString())
                    .SendAsync("NotificationDeleted", notification.Id);

                _notificationsRepository.DeleteNotification(notification);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_tasksRepository.DeleteTask(taskToDelete))
            {
                ModelState.AddModelError("", "Something went wrong deleting the task and its associated notification.");
                return StatusCode(500, ModelState);
            }

            // Notify clients about the deletion
            await _hubContext.Clients.User(taskToDelete.AssignedTo?.ToString())
     .SendAsync("TaskDeleted", id);

            return NoContent();
        }

        [HttpGet("assignedtouser/{userId}")]
        public IActionResult GetTasksAssignedToUser(int userId)
        {

            // Fetch tasks assigned to the user, excluding those where CreatedBy == AssignedTo
            var tasks = _tasksRepository.GetTasksAssignedToUser(userId)
                .Where(t => t.CreatedBy != t.AssignedTo)
                .ToList();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tasksDTO = tasks.Select(t => new TasksDTO
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedTo = t.AssignedTo,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                FilePath = t.FilePath,
                CreatedBy = t.CreatedBy
            }).ToList();

            return Ok(tasksDTO);
        }

        [HttpGet("createdByUser/{userId}")]
        public IActionResult GetTasksCreatedByUser(int userId)
        {
            var tasks = _tasksRepository.GetTasksCreatedByUser(userId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tasksDTO = tasks.Select(t => new TasksDTO
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedTo = t.AssignedTo,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                FilePath = t.FilePath,
                CreatedBy = t.CreatedBy
            }).ToList();

            return Ok(tasksDTO);
        }

    }
}
