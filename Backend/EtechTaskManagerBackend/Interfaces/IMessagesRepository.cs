using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Models;
using System.Collections.Generic;

namespace EtechTaskManagerBackend.Interfaces
{
    public interface IMessagesRepository
    {
        ICollection<Messages> GetAllMessages(); // Retrieve all messages
        ICollection<Messages> GetMessagesByUser(int userId); // Retrieve all messages for a specific user
        Messages GetMessageById(int messageId); // Retrieve a specific message by its ID
        ICollection<Messages> GetMessagesBetweenUsers(int senderId, int recipientId); // Retrieve messages between two users
        Messages GetLastMessageForContact(int userId, int contactId);

        bool SendMessage(Messages message); // Send a new message
        bool MarkMessageAsRead(int messageId); // Mark a specific message as read
        bool MarkAllMessagesAsRead(int userId); // Mark all messages for a user as read
        bool MarkMessagesAsReadBetweenUsers(int userId, int contactId);
        int GetUnreadCountBetweenUsers(int userId, int contactId);
        Dictionary<int, int> GetUnreadCountsByUser(int userId);

        bool DeleteMessage(Messages message); // Delete a specific message
        bool DeleteMessagesBetweenUsers(int senderId, int recipientId); // Delete all messages between two users
       // New methods:
        bool DeleteForMe(int messageId, int currentUserId);
        bool DeleteForEveryone(int messageId, int currentUserId);
        bool DeleteChatForMe(int userId, int contactId);

        bool Save(); // Save changes to the database
        bool MessageExists(int messageId); // Check if a specific message exists

        bool UploadFile(int senderId, int recipientId, string filePath);
        bool EditMessage(int messageId, string newText);


        // New methods for search functionality
        ICollection<Messages> SearchMessages(string query); // Search messages by content
        ICollection<UsersDTO> SearchUsers(string query);    // Search users by username or full name

    }
}
