using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.EtechHubs;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using EtechTaskManagerBackend.Repository;
using EtechTaskManagerBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EtechTaskManagerBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class NotificationsController : Controller
    {
        private readonly INotificationsRepository _notificationsRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly NotificationService _notificationService;

        public NotificationsController(INotificationsRepository notificationsRepository, IHubContext<NotificationHub> hubContext, NotificationService notificationService, IUsersRepository usersRepository)
        {
            _notificationsRepository = notificationsRepository;
            _hubContext = hubContext;
            _notificationService = notificationService;
            _usersRepository = usersRepository;
        }

        //GET: api/notifications
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Notifications>))]
        public IActionResult GetAllNotifications()
        {
            var notifications = _notificationsRepository.GetAllNotifications();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var notificationsDTO = notifications.Select(n => new NotificationsDTO
            {
                Id = n.Id,
                Message = n.Message,
                Recipient = n.Recipient,
                Type = n.Type,
                Date = n.Date,
                IsRead = n.IsRead,
                CreatedBy = n.CreatedBy
            }).ToList();

            return Ok(notificationsDTO);
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Notifications>))]
        public IActionResult GetAllNotificationsForAdmin([FromQuery] string createdBy)
        {
            var notifications = _notificationsRepository.GetAllNotificationsForAdmin(createdBy);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var notificationsDTO = notifications.Select(n => new NotificationsDTO
            {
                Id = n.Id,
                Message = n.Message,
                Recipient = n.Recipient,
                Type = n.Type,
                Date = n.Date,
                IsRead = n.IsRead,
                CreatedBy = n.CreatedBy
            }).ToList();

            return Ok(notificationsDTO);
        }


        // GET: api/notifications/{userId}
        [HttpGet("user/{userId}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<NotificationsDTO>))]
        [ProducesResponseType(404)]
        public IActionResult GetNotifications(int userId)
        {
            var notifications = _notificationsRepository.GetNotifications(userId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            //Mapping to DTO
            var notificationsDTO = notifications.Select(n => new NotificationsDTO
            {
                Id = n.Id,
                Message = n.Message,
                Recipient = n.Recipient,
                Type = n.Type,
                Date = n.Date,
                IsRead = n.IsRead,
                CreatedBy = n.CreatedBy
            }).ToList();

            return Ok(notificationsDTO);
        }


        // GET: api/notifications/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(NotificationsDTO))]
        [ProducesResponseType(404)]
        public IActionResult GetNotification(int id)
        {
            var notification = _notificationsRepository.GetNotification(id);
            if (notification == null)
            {
                return NotFound();
            }

            // Mapping to DTO
            var notificationDTO = new NotificationsDTO
            {
                Id = notification.Id,
                Message = notification.Message,
                Recipient = notification.Recipient,
                Type = notification.Type,
                Date = notification.Date,
                IsRead = notification.IsRead,
                CreatedBy = notification.CreatedBy
            };

            return Ok(notificationDTO);
        }

        // GET: api/notifications/user/{userId}/unreadcount
        [HttpGet("user/{userId}/unreadcount")]
        [ProducesResponseType(200, Type = typeof(int))]
        [ProducesResponseType(404)]
        public IActionResult GetUnreadNotificationCount(int userId)
        {
            var unreadCount = _notificationsRepository.GetUnreadNotificationCount(userId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(new { unreadCount }); // Returns the unread count in JSON format
        }

        [HttpPost]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateNotification([FromBody] NotificationsDTO notificationCreate)
        {

            if (notificationCreate == null)
            {
                return BadRequest(ModelState);
            }

            var recipientUser = _usersRepository.GetUserById(notificationCreate.Recipient ?? 0);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Cannot send notifications to a banned user.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Manual mapping from UsersDTO to Users
            var NotificationsMap = new Notifications
            {
                Id = notificationCreate.Id,
                Message = notificationCreate.Message,
                Recipient = notificationCreate.Recipient,
                Type = notificationCreate.Type,
                Date = notificationCreate.Date,
                IsRead = notificationCreate.IsRead,
                CreatedBy = notificationCreate.CreatedBy
            };

            // Log asynchronously
            await _notificationService.LogNotificationDetails(NotificationsMap);

            try
            {
                if (!_notificationsRepository.AddNotification(NotificationsMap))
                {
                    ModelState.AddModelError("", "Something went wrong while saving");
                    return StatusCode(500, ModelState);
                }

                // Notify the specific user via SignalR
                await _hubContext.Clients.User(NotificationsMap.Recipient.ToString())
                    .SendAsync("ReceiveNotification", NotificationsMap.Id, NotificationsMap.Recipient, NotificationsMap.Message, NotificationsMap.Type, NotificationsMap.Date, NotificationsMap.IsRead);

                return Ok("Notification successfully created and sent.");
            }
            catch (DbUpdateException ex)
            {
                // Log the inner exception for debugging
                var innerExceptionMessage = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError("", $"Database update error: {innerExceptionMessage}");
                return StatusCode(500, ModelState);
            }

            return Ok("Successfully created");
        }


        // PUT: api/notifications/{id}/read
        [HttpPut("{id}/read")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult MarkAsRead(int id)
        {
            var notification = _notificationsRepository.GetNotification(id);
            if (notification == null)
            {
                return NotFound("Notification not found.");
            }

            // Check if the user is banned
            var recipientUser = _usersRepository.GetUserById(notification.Recipient ?? 0);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Banned users cannot mark notifications as read.");
            }

            var result = _notificationsRepository.MarkAsRead(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent(); // HTTP 204 No Content
        }

        // PUT: api/notifications/{userId}/markallread
        [HttpPut("{userId}/markallread")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult MarkAllAsRead(int userId)
        {

            // Check if the user is banned
            var recipientUser = _usersRepository.GetUserById(userId);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Banned users cannot mark notifications as read.");
            }

            var result = _notificationsRepository.MarkAllAsRead(userId);
            if (!result)
            {
                return NotFound(); // No unread notifications or user not found
            }

            return NoContent(); // HTTP 204 No Content
        }

        // PUT: api/notifications/{id}
        [HttpPut("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateNotification(int id, [FromBody] NotificationsDTO updatedNotifications)
        {
            if (updatedNotifications == null)
            {
                return BadRequest(ModelState);
            }

            // Check if the notification exists
            var existingNotification = _notificationsRepository.GetNotification(id);
            if (existingNotification == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return NotFound();
            }

            // Check if the user is banned
            var recipientUser = _usersRepository.GetUserById(existingNotification.Recipient ?? 0);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Banned users cannot delete notifications.");
            }

            // Update the properties of the existing notification
            existingNotification.Message = updatedNotifications.Message;
            existingNotification.Recipient = updatedNotifications.Recipient;
            existingNotification.Type = updatedNotifications.Type;
            existingNotification.Date = updatedNotifications.Date;
            existingNotification.IsRead = updatedNotifications.IsRead;
            existingNotification.CreatedBy = updatedNotifications.CreatedBy;

            if (!_notificationsRepository.UpdateNotification(existingNotification))
            {
                ModelState.AddModelError("", "Something went wrong while saving");
                return StatusCode(500, ModelState);
            }

            // Notify clients of the update
            await _hubContext.Clients.User(existingNotification.Recipient.ToString())
                .SendAsync("NotificationUpdated", existingNotification.Id, existingNotification.Message, existingNotification.Date);

            await _notificationService.LogNotificationDetails(existingNotification);

            return NoContent();
        }

        // DELETE: api/notifications/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            if (!_notificationsRepository.NotificationExists(id))
            {
                return NotFound();
            }

            var notificationToDelete = _notificationsRepository.GetNotification(id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if the user is banned
            var recipientUser = _usersRepository.GetUserById(notificationToDelete.Recipient ?? 0);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Banned users cannot delete notifications.");
            }

            if (!_notificationsRepository.DeleteNotification(notificationToDelete))
            {
                ModelState.AddModelError("", "Something went wrong deleting notification");
            }

            // Notify clients of the deletion
            await _hubContext.Clients.User(notificationToDelete.Recipient.ToString())
                .SendAsync("NotificationDeleted", notificationToDelete.Id);

            return NoContent();
        }

        // DELETE: api/notifications/user/{userId}/deleteall
        [HttpDelete("user/{id}/deleteall")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult DeleteAllNotifications(int id)
        {
            // Check if the user is banned
            var recipientUser = _usersRepository.GetUserById(id);
            if (recipientUser == null || recipientUser.Banned)
            {
                return BadRequest("Banned users cannot delete notifications.");
            }

            var result = _notificationsRepository.DeleteAllNotifications(id);

            if (!result)
            {
                return NotFound(); // No notifications found for the user
            }

            return NoContent(); // HTTP 204 No Content
        }


    }
}