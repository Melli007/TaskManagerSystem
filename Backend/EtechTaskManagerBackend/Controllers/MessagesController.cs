using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.EtechHubs;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Linq;

namespace EtechTaskManagerBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IMessagesRepository _messagesRepository;
        private readonly IWebHostEnvironment _env; // <-- Added
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly IUsersRepository _usersRepository;

        public MessagesController(IMessagesRepository messagesRepository, IWebHostEnvironment env, IHubContext<MessageHub> hubContext, IUsersRepository usersRepository)
        {
            _messagesRepository = messagesRepository;
            _env = env;
            _hubContext = hubContext;
            _usersRepository = usersRepository;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Messages>))]
        public IActionResult GetAllMessages()
        {
            var messages = _messagesRepository.GetAllMessages();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(messages);
        }

        [HttpGet("{messageId}")]
        [ProducesResponseType(200, Type = typeof(Messages))]
        [ProducesResponseType(400)]
        public IActionResult GetMessageById(int messageId)
        {
            if (!_messagesRepository.MessageExists(messageId))
            {
                return NotFound();
            }

            var message = _messagesRepository.GetMessageById(messageId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(message);
        }

        [HttpGet("{userId}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Messages>))]
        [ProducesResponseType(400)]
        public IActionResult GetMessagesByUser(int userId)
        {

            var messages = _messagesRepository.GetMessagesByUser(userId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(messages);
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Messages>))]
        [ProducesResponseType(400)]
        public IActionResult GetMessagesBetweenUsers([FromQuery] int senderId, [FromQuery] int recipientId)
        {
            var messages = _messagesRepository.GetMessagesBetweenUsers(senderId, recipientId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(messages);
        }

        [HttpPost]
        [Consumes("multipart/form-data")] // This is key for Swagger + binding
        public async Task<IActionResult> CreateMessage([FromForm] MessagesDTO messagesCreate)
        {
            try
            {

                var senderCheck = CheckIfUserIsBanned(messagesCreate.SenderId);
                if (senderCheck != null) return senderCheck;

                var recipientCheck = CheckIfUserIsBanned(messagesCreate.RecipientId);
                if (recipientCheck != null) return recipientCheck;

                // 1) Basic checks
                if (messagesCreate.SenderId <= 0 || messagesCreate.RecipientId <= 0)
                    return BadRequest("SenderId and RecipientId must be > 0.");

                // If user didn't type a text but included a file => default to "File upload"
                if (string.IsNullOrWhiteSpace(messagesCreate.Message) && messagesCreate.File != null)
                {
                    messagesCreate.Message = "File upload";
                }

                // Setup filePath if file is uploaded
                string? filePath = null;
                if (messagesCreate.File != null && messagesCreate.File.Length > 0)
                {
                    // Example allowed extensions
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".mp4", ".avi", ".mov" };
                    var fileExtension = Path.GetExtension(messagesCreate.File.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                        return BadRequest("Invalid file extension. Allowed: PDF, JPG, JPEG, PNG, MP4, AVI, MOV");

                    // Optional: Check magic numbers
                    if (!IsValidFileContent(messagesCreate.File.OpenReadStream(), fileExtension))
                        return BadRequest("Invalid file content (magic number check failed).");

                    // Optional: file size limit
                    long maxFileSize = 16 * 1024 * 1024; // 5 MB
                    if (messagesCreate.File.Length > maxFileSize)
                        return BadRequest("File size exceeds 5 MB limit.");

                    // Save file
                    var apiBase = "https://localhost:7013";
                    var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(messagesCreate.File.FileName)}{fileExtension}";
                    var uploadPath = Path.Combine(_env.WebRootPath, "SecureUploads_Messages");
                    Directory.CreateDirectory(uploadPath);

                    // Physical path
                    var physicalPath = Path.Combine(uploadPath, safeFileName);

                    using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write))
                    {
                        await messagesCreate.File.CopyToAsync(stream);
                    }

                    // Store the final path in the DTO, or just a relative path
                    filePath = $"{apiBase}/SecureUploads_Messages/{safeFileName}";
                    messagesCreate.FilePath = filePath;
                }

                // Now store a record in DB via your repository
                var newMessage = new Messages
                {
                    SenderId = messagesCreate.SenderId,
                    RecipientId = messagesCreate.RecipientId,
                    Message = messagesCreate.Message ?? string.Empty,
                    FilePath = messagesCreate.FilePath,
                    SentAt = DateTime.Now,
                    IsRead = false
                };

                if (_messagesRepository.SendMessage(newMessage))
                {
                    // 3) Real-time notifications
                    //    a) Send "ReceiveMessage" to recipient
                    await _hubContext.Clients.User(messagesCreate.RecipientId.ToString())
                        .SendAsync("ReceiveMessage", messagesCreate.SenderId.ToString(), messagesCreate.Message, messagesCreate.FilePath);

                    //    b) Update contact list for both
                    await _hubContext.Clients.Users(messagesCreate.SenderId.ToString(), messagesCreate.RecipientId.ToString())
                        .SendAsync("ContactListUpdated", messagesCreate.SenderId.ToString(), messagesCreate.RecipientId.ToString(),
                                    messagesCreate.Message, messagesCreate.FilePath);

                    //    c) Optionally push unread count directly
                    await _hubContext.Clients.User(messagesCreate.RecipientId.ToString())
                        .SendAsync("UpdateUnreadCounts");  // or use "NotifyUnreadCount"

                    return Ok(new
                    {
                        success = true,
                        Message = "Message created successfully.",
                        FilePath = filePath
                    });
                }
                else
                {
                    ModelState.AddModelError("", "Failed to save message in DB");
                    return StatusCode(500, ModelState);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // Example magic number check, like in your tasks
        private bool IsValidFileContent(Stream fileStream, string fileExtension)
        {
            var magicNumbers = new Dictionary<string, byte[]>
            {
                { ".pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 } },
                { ".jpg",  new byte[] { 0xFF, 0xD8 } },
                { ".jpeg", new byte[] { 0xFF, 0xD8 } }, // same as .jpg
                { ".png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
                { ".mp4", new byte[] { 0x00, 0x00, 0x00, 0x20 } }, // Updated MP4 magic number
                { ".avi",  new byte[] { 0x52, 0x49, 0x46, 0x46 } },
                { ".mov",  new byte[] { 0x00, 0x00, 0x00, 0x14 } } 
                // Note: actual .mov magic can vary, you might 
                // want a more robust check or skip for .mov
            };

            if (magicNumbers.ContainsKey(fileExtension))
            {
                byte[] buffer = new byte[magicNumbers[fileExtension].Length];
                fileStream.Position = 0;
                fileStream.Read(buffer, 0, buffer.Length);
                fileStream.Position = 0;

                return buffer.SequenceEqual(magicNumbers[fileExtension]);
            }

            // If extension not in dictionary, or uncertain => block or allow
            return false;
        }


        [HttpPut("{messageId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult MarkMessageAsRead(int messageId)
        {
            if (!_messagesRepository.MessageExists(messageId))
            {
                return NotFound();
            }

            if (!_messagesRepository.MarkMessageAsRead(messageId))
            {
                ModelState.AddModelError("", "Failed to mark the message as read.");
                return StatusCode(500, ModelState);
            }

            return Ok("Message marked as read.");
        }

        [HttpPut("{userId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult MarkAllMessagesAsRead(int userId)
        {
            if (!_messagesRepository.MarkAllMessagesAsRead(userId))
            {
                return BadRequest("No unread messages found for the user.");
            }

            return Ok("All messages marked as read.");
        }

        [HttpPut]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> MarkMessagesAsReadBetweenUsers(int userId, int contactId)
        {
            try
            {
                var userCheck = CheckIfUserIsBanned(userId);
                if (userCheck != null) return userCheck;

                var contactCheck = CheckIfUserIsBanned(contactId);
                if (contactCheck != null) return contactCheck;

                var updated = _messagesRepository.MarkMessagesAsReadBetweenUsers(userId, contactId);

                if (!updated)
                {
                    return BadRequest("No unread messages found between the specified users.");
                }

                // Optionally also update unread counts for both
                await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCounts");
                await _hubContext.Clients.User(contactId.ToString()).SendAsync("UpdateUnreadCounts");

                return Ok("Messages between users marked as read.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{userId}/{contactId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult GetUnreadCountBetweenUsers(int userId, int contactId)
        {
            try
            {
                // Call the repository method
                var unreadCount = _messagesRepository.GetUnreadCountBetweenUsers(userId, contactId);

                return Ok(new { UnreadCount = unreadCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{userId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> NotifyUnreadCount(int userId)
        {
            try
            {
                var dict = _messagesRepository.GetUnreadCountsByUser(userId);
                // dict => { 1066 => 2,  999 => 5, etc. }

                // Transform each KeyValuePair into { contactId, count }
                var data = dict
                    .Select(kvp => new { contactId = kvp.Key, count = kvp.Value })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data // => [ { contactId: 1066, count: 2 }, { contactId: 999, count: 5 } ]
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpDelete("{messageId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult DeleteMessage(int messageId)
        {
            if (!_messagesRepository.MessageExists(messageId))
            {
                return NotFound();
            }

            var messageToDelete = _messagesRepository.GetMessageById(messageId);

            var senderCheck = CheckIfUserIsBanned(messageToDelete.SenderId);
            if (senderCheck != null) return senderCheck;

            var recipientCheck = CheckIfUserIsBanned(messageToDelete.RecipientId);
            if (recipientCheck != null) return recipientCheck;

            if (!_messagesRepository.DeleteMessage(messageToDelete))
            {
                ModelState.AddModelError("", "Failed to delete the message.");
                return StatusCode(500, ModelState);
            }

            return NoContent();
        }


        [HttpDelete]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public IActionResult DeleteMessagesBetweenUsers([FromQuery] int senderId, [FromQuery] int recipientId)
        {
            if (!_messagesRepository.DeleteMessagesBetweenUsers(senderId, recipientId))
            {
                return BadRequest("No messages found between the specified users.");
            }

            return NoContent();
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<UsersDTO>))]
        [ProducesResponseType(400)]
        public IActionResult SearchUsers([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query cannot be empty.");
                }

                var users = _usersRepository.GetUsers()
                    .Where(u => u.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                u.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Messages>))]
        [ProducesResponseType(400)]
        public IActionResult SearchMessages([FromQuery] string query, [FromQuery] int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query cannot be empty.");
                }

                var messages = _messagesRepository.SearchMessages(query)
                    .Where(m => (m.SenderId == userId || m.RecipientId == userId) && !m.IsDeletedForEveryone)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPatch]
public async Task<IActionResult> EditMessage([FromQuery] int messageId, [FromQuery] string newText)
{
    // Check if the message exists
    if (!_messagesRepository.MessageExists(messageId))
        return NotFound($"Message with ID {messageId} not found.");

    // Retrieve the message to get sender and recipient IDs
    var message = _messagesRepository.GetMessageById(messageId);
    if (message == null)
        return NotFound("Message not found.");

            // Check if the sender is banned
            var senderCheck = CheckIfUserIsBanned(message.SenderId);
            if (senderCheck != null) return senderCheck;

            // Check if the recipient is banned
            var recipientCheck = CheckIfUserIsBanned(message.RecipientId);
            if (recipientCheck != null) return recipientCheck;

            // Update the message in the database
            bool updated = _messagesRepository.EditMessage(messageId, newText);
    if (!updated)
        return StatusCode(500, "Failed to edit the message in the database.");

    // Notify sender and recipient about the edited message via SignalR
    await _hubContext.Clients.Users(message.SenderId.ToString(), message.RecipientId.ToString())
        .SendAsync("MessageEdited", messageId, newText);

    return Ok("Message edited successfully.");
}


        [HttpPatch]
        public async Task<IActionResult> DeleteForMe([FromQuery] int messageId, [FromQuery] int userId)
        {
            // Check if the user is banned
            var userCheck = CheckIfUserIsBanned(userId);
            if (userCheck != null) return userCheck;


            bool success = _messagesRepository.DeleteForMe(messageId, userId);
            if (!success)
                return BadRequest("Failed to delete for me, or you are not the sender/recipient.");

            await _hubContext.Clients.User(userId.ToString())
           .SendAsync("MessageDeletedForMe", messageId);

            return Ok("Message hidden for current user only.");
        }

        [HttpPatch]
        public async Task<IActionResult> DeleteForEveryone([FromQuery] int messageId, [FromQuery] int userId)
        {

            // Verify if the message exists
            if (!_messagesRepository.MessageExists(messageId))
            {
                return NotFound("Message not found.");
            }

            // Retrieve the message details
            var message = _messagesRepository.GetMessageById(messageId);
            if (message == null)
            {
                return NotFound("Message not found.");
            }

            // Check if the user is authorized to delete the message
            if (message.SenderId != userId)
            {
                return Forbid("You are not authorized to delete this message.");
            }

            // Check if the sender (userId) is banned
            var senderCheck = CheckIfUserIsBanned(userId);
            if (senderCheck != null) return senderCheck;

            // Check if the recipient is banned
            var recipientCheck = CheckIfUserIsBanned(message.RecipientId);
            if (recipientCheck != null) return recipientCheck;

            // Mark the message as deleted for everyone
            var success = _messagesRepository.DeleteForEveryone(messageId, userId);
            if (!success)
            {
                return BadRequest("Failed to delete the message for everyone.");
            }

            // Notify both sender and recipient about the deletion via SignalR
            await _hubContext.Clients.Users(message.SenderId.ToString(), message.RecipientId.ToString())
                .SendAsync("MessageDeleted", messageId);

            return Ok("Message deleted for everyone.");
        }


        [HttpPatch("{userId}/{contactId}")]
        public IActionResult DeleteChatForMe(int userId, int contactId)
        {
            var userCheck = CheckIfUserIsBanned(userId);
            if (userCheck != null) return userCheck;

            bool success = _messagesRepository.DeleteChatForMe(userId, contactId);

            if (!success)
                return BadRequest("No messages found or failed to delete the chat for the user.");

            return Ok("Chat deleted for the current user.");
        }

        [HttpGet("{userId}/{contactId}")]
        [ProducesResponseType(200, Type = typeof(Messages))]
        [ProducesResponseType(404)]
        public IActionResult GetLastMessageForContact(int userId, int contactId)
        {
            var message = _messagesRepository.GetLastMessageForContact(userId, contactId);

            if (message == null)
            {
                return NotFound("No messages found between the specified users.");
            }

            return Ok(message);
        }

        private IActionResult CheckIfUserIsBanned(int userId)
        {
            var user = _usersRepository.GetUserById(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (user.Banned)
            {
                return BadRequest("Action not allowed. The user is banned.");
            }

            return null; // No issues
        }

    }
}
