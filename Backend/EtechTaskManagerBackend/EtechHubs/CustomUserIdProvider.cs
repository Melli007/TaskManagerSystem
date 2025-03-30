using Microsoft.AspNetCore.SignalR;

namespace EtechTaskManagerBackend.EtechHubs
{
    // Custom user identifier provider
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {// Extract the userId from the query string
            var userId = connection.GetHttpContext()?.Request.Query["userId"].ToString();

            // Optional: Log for debugging
            Console.WriteLine($"Extracted userId: {userId}");

            return userId ?? string.Empty;
        }
    }
}
