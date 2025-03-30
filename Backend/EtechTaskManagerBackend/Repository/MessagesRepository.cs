using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace EtechTaskManagerBackend.Repository
{
    public class MessagesRepository : IMessagesRepository
    {
        private readonly DataContext _context;
        private readonly IUsersRepository _usersRepository;

        public MessagesRepository(DataContext context, IUsersRepository usersRepository)
        {
            _context = context;
            _usersRepository = usersRepository;
        }

        public ICollection<Messages> GetAllMessages()
        {
            return _context.Messages.OrderBy(m => m.MID).ToList();
        }

        public Messages GetMessageById(int messageId)
        {
            return _context.Messages.FirstOrDefault(m => m.MID == messageId);
        }

        public ICollection<Messages> GetMessagesByUser(int userId)
        {
            return _context.Messages
                .Where(m => m.SenderId == userId || m.RecipientId == userId)
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        public ICollection<Messages> GetMessagesBetweenUsers(int senderId, int recipientId)
        {
            return _context.Messages
                .Where(m => (m.SenderId == senderId && m.RecipientId == recipientId) ||
                            (m.SenderId == recipientId && m.RecipientId == senderId))
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        public bool SendMessage(Messages message)
        {
            message.IsRead = false; // Explicitly set IsRead to false
            _context.Messages.Add(message);

            // Get the sender's full name using the UsersRepository
            var sender = _usersRepository.GetUserById(message.SenderId);
            string senderFullName = sender?.FullName ?? "Unknown";

            // Create a notification for the recipient
            var notification = new Notifications
            {
                Message = $"Ju keni marrë një mesazh të ri: \"{message.Message}\". Nga {senderFullName}.",
                Recipient = message.RecipientId,
                Type = "Message",
                Date = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);

            return Save();
        }

        public bool MarkMessageAsRead(int messageId)
        {
            var message = GetMessageById(messageId);
            if (message == null) return false;

            message.IsRead = true;
            message.ReadAt = System.DateTime.Now;
            return Save();
        }

        public bool MarkAllMessagesAsRead(int userId)
        {
            var unreadMessages = _context.Messages
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .ToList();

            if (!unreadMessages.Any()) return false;

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = System.DateTime.Now;
            }

            return Save();
        }

        public bool DeleteMessage(Messages message)
        {
            _context.Messages.Remove(message);
            return Save();
        }

        public bool DeleteMessagesBetweenUsers(int senderId, int recipientId)
        {
            var messages = _context.Messages
                .Where(m => (m.SenderId == senderId && m.RecipientId == recipientId) ||
                            (m.SenderId == recipientId && m.RecipientId == senderId))
                .ToList();

            if (!messages.Any()) return false;

            _context.Messages.RemoveRange(messages);
            return Save();
        }

        public bool MessageExists(int messageId)
        {
            return _context.Messages.Any(m => m.MID == messageId);
        }

        public bool UploadFile(int senderId, int recipientId, string filePath)
        {
            var fileMessage = new Messages
            {
                SenderId = senderId,
                RecipientId = recipientId,
                Message = "File upload",
                FilePath = filePath,
                IsRead = false,
                SentAt = DateTime.Now
            };

            _context.Messages.Add(fileMessage);
            return Save();
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0;
        }

        public bool MarkMessagesAsReadBetweenUsers(int userId, int contactId)
        {
            // Find messages where the recipient is the user and sender is the contact, and they are unread
            var messagesToMark = _context.Messages
               .Where(m =>
            // Must be between these two users:
            ((m.SenderId == userId && m.RecipientId == contactId) ||
             (m.SenderId == contactId && m.RecipientId == userId))
            // AND must be visible to this current user
            && (
                (m.SenderId == userId && m.IsVisibleToSender) ||
                (m.RecipientId == userId && m.IsVisibleToRecipient)
            )
        )
                .ToList();

            if (!messagesToMark.Any())
            {
                return false;
            }

            foreach (var message in messagesToMark)
            {
                message.IsRead = true;
            }

            _context.SaveChanges();
            return true;
        }

        public int GetUnreadCountBetweenUsers(int userId, int contactId)
        {
            // Count unread messages where the recipient is the current user (userId)
            // and the sender is the contact (contactId)
            return _context.Messages
                .Where(m => m.RecipientId == userId && m.SenderId == contactId && !m.IsRead)
                .Count();
        }
        public Dictionary<int, int> GetUnreadCountsByUser(int userId)
        {
            return _context.Messages
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public ICollection<Messages> SearchMessages(string query)
        {
            if (string.IsNullOrEmpty(query))
                return new List<Messages>();

            // Use EF.Functions.Like for case-insensitive search
            return _context.Messages
                .Where(m => !string.IsNullOrEmpty(m.Message) && EF.Functions.Like(m.Message, $"%{query}%") && !m.IsDeletedForEveryone)// Exclude deleted messages
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        public ICollection<UsersDTO> SearchUsers(string query)
        {
            if (string.IsNullOrEmpty(query))
                return new List<UsersDTO>();

            var users = _usersRepository.GetUsers();
            return users
                .Where(u => !string.IsNullOrEmpty(u.Username) && u.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            !string.IsNullOrEmpty(u.FullName) && u.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(u => new UsersDTO
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    IsOnline = u.IsOnline,
                    Role = u.Role
                })
                .ToList();
        }
        public bool EditMessage(int messageId, string newText)
        {
            var message = GetMessageById(messageId);
            if (message == null)
                return false;

            // Retrieve the sender's full name
            var sender = _usersRepository.GetUserById(message.SenderId);
            var senderFullName = sender?.FullName ?? "Unknown User";

            // The original notification format
            var originalNotificationMessage = $"Ju keni marrë një mesazh të ri: \"{message.Message}\". Nga {senderFullName}.";

            // Find the notification associated with this message
            var notification = _context.Notifications
                .FirstOrDefault(n =>
                    n.Recipient == message.RecipientId &&
                    n.Type == "Message" &&
                    n.Message == originalNotificationMessage // Exact match with original notification message
                );

            if (notification != null)
            {
                // Update the existing notification
                notification.Message = $"Mesazhi i redaktuar nga {senderFullName}: \"{newText}\".";
                _context.Notifications.Update(notification);
            }
            else
            {
                // Log or handle the case where the notification was not found
                Console.WriteLine("Notification not found for the edited message.");
            }

            // Update the message itself
            message.Message = newText;
            message.IsEdited = true;

            return Save(); // Save changes to the database
        }

        public bool DeleteForMe(int messageId, int currentUserId)
        {
            // 1) Fetch the message
            var message = _context.Messages.FirstOrDefault(m => m.MID == messageId);
            if (message == null)
                return false;

            // 2) Determine if currentUser is sender or recipient
            if (message.SenderId == currentUserId)
            {
                // Hides from sender
                message.IsVisibleToSender = false;
            }
            else if (message.RecipientId == currentUserId)
            {
                // Hides from recipient
                message.IsVisibleToRecipient = false;
            }
            else
            {
                // Not a participant => do nothing
                return false;
            }

            // 3) Save
            _context.Messages.Update(message);
            return Save(); // This calls _context.SaveChanges(), presumably
        }

        public bool DeleteForEveryone(int messageId, int currentUserId)
        {
            var message = _context.Messages.FirstOrDefault(m => m.MID == messageId);
            if (message == null)
                return false;

            // Retrieve the sender's full name
            var sender = _usersRepository.GetUserById(message.SenderId);
            var senderFullName = sender?.FullName ?? "Unknown User";

            // Construct the original and edited notification formats
            var originalNotificationMessage = $"Ju keni marrë një mesazh të ri: \"{message.Message}\". Nga {senderFullName}.";
            var editedNotificationMessage = $"Mesazhi i redaktuar nga {senderFullName}: \"{message.Message}\".";

            // Find the notification associated with this message (either original or edited)
            var notification = _context.Notifications
                .FirstOrDefault(n =>
                    n.Recipient == message.RecipientId &&
                    n.Type == "Message" &&
                    (n.Message == originalNotificationMessage || n.Message == editedNotificationMessage) // Match either
                );

            if (notification != null)
            {
                // Update the notification message to indicate the message has been deleted
                notification.Message = $"Mesazhi nga {senderFullName} është fshirë.";
                _context.Notifications.Update(notification);
            }
            else
            {
                // Log or handle cases where the notification was not found
                Console.WriteLine("Notification not found for the deleted message.");
            }

            // Mark the message as deleted for everyone
            message.IsDeletedForEveryone = true;

            _context.Messages.Update(message);
            return Save(); // Save changes to the database
        }


        public bool DeleteChatForMe(int userId, int contactId)
        {
            var messages = _context.Messages
                .Where(m =>
                    (m.SenderId == userId && m.RecipientId == contactId && m.IsVisibleToSender) ||
                    (m.SenderId == contactId && m.RecipientId == userId && m.IsVisibleToRecipient))
                .ToList();

            if (!messages.Any())
                return false;

            foreach (var message in messages)
            {
                if (message.SenderId == userId)
                    message.IsVisibleToSender = false;

                if (message.RecipientId == userId)
                    message.IsVisibleToRecipient = false;
            }

            _context.Messages.UpdateRange(messages);
            return Save();
        }

        public Messages GetLastMessageForContact(int userId, int contactId)
        {
            return _context.Messages
                .Where(m =>
                    ((m.SenderId == userId && m.RecipientId == contactId && m.IsVisibleToSender) ||
                     (m.SenderId == contactId && m.RecipientId == userId && m.IsVisibleToRecipient)) &&
                    !m.IsDeletedForEveryone // Exclude messages deleted for everyone
                )
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();
        }



    }
}
