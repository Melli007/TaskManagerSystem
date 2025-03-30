using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EtechTaskManagerBackend.EtechHubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        // Optional: Store user connections if needed
        private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        // Handle user connections
        public override async Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"];

            if (!string.IsNullOrEmpty(userId))
            {
                // Add user connection
                UserConnections.AddOrUpdate(userId,
                    _ => new HashSet<string> { Context.ConnectionId },
                    (_, connections) =>
                    {
                        connections.Add(Context.ConnectionId);
                        return connections;
                    });

                _logger.LogInformation($"User {userId} connected with connectionId {Context.ConnectionId}");
            }
            else
            {
                _logger.LogWarning("UserId not found in connection query.");
            }

            await base.OnConnectedAsync();
        }

        // Handle user disconnections
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"];

            if (!string.IsNullOrEmpty(userId))
            {
                if (UserConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(Context.ConnectionId);

                    if (connections.Count == 0)
                    {
                        UserConnections.TryRemove(userId, out _);
                    }
                }

                _logger.LogInformation($"User {userId} disconnected from connectionId {Context.ConnectionId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Send a new notification to a specific user
        public async Task SendNotificationToUser(string userId, int notificationId, string message, string type, DateTime date, bool isRead)
        {
            _logger.LogInformation($"Sending notification to user {userId}: {message}");

            await Clients.User(userId).SendAsync("ReceiveNotification", notificationId, userId, message, type, date, isRead);

            _logger.LogInformation($"Notification {notificationId} sent to user {userId}");
        }

        // Notify a user about a notification update
        public async Task NotifyNotificationUpdated(string userId, int notificationId, string message, DateTime date)
        {
            _logger.LogInformation($"Updating notification {notificationId} for user {userId}");

            await Clients.User(userId).SendAsync("NotificationUpdated", notificationId, message, date);

            _logger.LogInformation($"Notification {notificationId} updated for user {userId}");
        }

        // Notify a user about a notification deletion
        public async Task NotifyNotificationDeleted(string userId, int notificationId)
        {
            _logger.LogInformation($"Deleting notification {notificationId} for user {userId}");

            await Clients.User(userId).SendAsync("NotificationDeleted", notificationId);

            _logger.LogInformation($"Notification {notificationId} deleted for user {userId}");
        }
    }
}
