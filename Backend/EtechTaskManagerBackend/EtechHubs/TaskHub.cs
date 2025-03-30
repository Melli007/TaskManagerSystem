using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EtechTaskManagerBackend.EtechHubs
{
    public class TaskHub : Hub
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    UserConnections.AddOrUpdate(userId,
                        _ => new HashSet<string> { Context.ConnectionId },
                        (_, connections) =>
                        {
                            connections.Add(Context.ConnectionId);
                            return connections;
                        });

                    Console.WriteLine($"User {userId} connected with connectionId {Context.ConnectionId}");
                }
                else
                {
                    Console.WriteLine("UserId not found in connection query.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectedAsync: {ex.Message}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

            if (!string.IsNullOrEmpty(userId))
            {
                // Remove the connectionId from the user's connections
                if (UserConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(Context.ConnectionId);

                    if (connections.Count == 0)
                    {
                        UserConnections.TryRemove(userId, out _);
                    }
                }

                Console.WriteLine($"User {userId} disconnected from connectionId {Context.ConnectionId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendTasksToUser(string userId, int id, string title, string description, string status, DateTime createdAt, DateTime dueDate, string createdByName, string filePath)
        {
            await Clients.User(userId).SendAsync("ReceiveTask", id, title, description, status, createdAt, dueDate, createdByName, filePath);
        }

        public async Task SendTaskUpdate(string userId, int totalTasks, int completedTasks, int inprogress, int pendingTasks, int nodeadlineTasks)
        {
            await Clients.User(userId).SendAsync("ReceiveTaskUpdate", totalTasks, completedTasks, inprogress, pendingTasks, nodeadlineTasks);
        }

        public async Task NotifyTaskUpdated(string userId, int id, string title, string description, string status, DateTime dueDate, string filePath)
        {
            await Clients.User(userId).SendAsync("TaskUpdated", id, title, description, status, dueDate, filePath);
        }

        public async Task NotifyTaskStatusChanged(string userId, int id, string newStatus)
        {
            await Clients.User(userId).SendAsync("UpdateTaskStatus", id, newStatus);
        }

        public async Task NotifyTaskDeleted(string userId, int id)
        {
            await Clients.User(userId).SendAsync("TaskDeleted", id);
        }
    }
}
