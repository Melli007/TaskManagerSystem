using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace EtechTaskManagerBackend.EtechHubs
{
    public class MessageHub : Hub
    {
        private readonly ILogger<MessageHub> _logger;

        public MessageHub(ILogger<MessageHub> logger)
        {
            _logger = logger;
        }

        // 1) OnConnected => Mark user as Online
        public override async Task OnConnectedAsync()
        {
            // Example: parse userId from query string (if you're passing userId=? in withUrl(...) on the client)
            var userIdStr = Context.GetHttpContext()?.Request.Query["userId"];

            if (!string.IsNullOrEmpty(userIdStr))
            {
                _logger.LogInformation($"User connected, userId: {userIdStr}");

                // Optionally set the user’s online status in your DB:
                //   await _userService.UpdateOnlineStatusAsync(userIdStr, true);

                // 2) Broadcast to all clients that userIdStr is now online
                await Clients.All.SendAsync("UserStatusChanged", userIdStr, true);
            }

            await base.OnConnectedAsync();
        }

        // 2) OnDisconnected => Mark user as Offline
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdStr = Context.GetHttpContext()?.Request.Query["userId"];

            if (!string.IsNullOrEmpty(userIdStr))
            {
                _logger.LogInformation($"User disconnected, userId: {userIdStr}");

                // Optionally set the user’s online status in your DB:
                //   await _userService.UpdateOnlineStatusAsync(userIdStr, false);

                // Broadcast to all clients that userIdStr is now offline
                await Clients.All.SendAsync("UserStatusChanged", userIdStr, false);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string senderId, string recipientId, string message, string filePath = null)
        {
            // Notify the recipient (if connected)
            await Clients.User(recipientId).SendAsync("ReceiveMessage", senderId, message, filePath);

            // Notify the sender for confirmation
            await Clients.User(senderId).SendAsync("MessageSent", message, filePath);

            // 3) If you want the client to fetch counts, just send a one-way event:
            await Clients.User(recipientId).SendAsync("UpdateUnreadCounts");

            // If you also do contact list updates:
            await Clients.Users(senderId, recipientId)
                .SendAsync("ContactListUpdated", senderId, recipientId, message, filePath);
        }
        public async Task EditMessage(string senderId, string recipientId, int messageId, string newText)
        {
            // Notify both sender and recipient about the edited message
            await Clients.Users(senderId, recipientId).SendAsync("MessageEdited", messageId, newText);
        }
        public async Task DeleteMessage(string senderId, string recipientId, int messageId)
        {
            // Notify both sender and recipient about the deleted message
            await Clients.Users(senderId, recipientId).SendAsync("MessageDeleted", messageId);
        }
        public async Task NotifyContactListUpdate(string senderId, string recipientId, string message, string filePath = null)
        {
            // Notify both users (sender and recipient) to update their contact list
            await Clients.Users(senderId, recipientId)
                .SendAsync("ContactListUpdated", senderId, recipientId, message, filePath);
        }
        public async Task NotifyMessageDeletedForMe(string userId, int messageId)
        {
            // Notify the user that a specific message was deleted for them
            await Clients.User(userId).SendAsync("MessageDeletedForMe", messageId);
        }

        public async Task NotifyUnreadCount(int userId)
        {
            // Notify the user about updated unread counts
            await Clients.User(userId.ToString())
                .SendAsync("UpdateUnreadCounts");
        }

        public async Task NotifyMessagesRead(int userId, int contactId)
        {
            // Notify both users about the read status
            await Clients.Users(userId.ToString(), contactId.ToString())
                .SendAsync("MessagesRead", userId, contactId);
        }

    }
}
