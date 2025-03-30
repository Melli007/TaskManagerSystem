using System;
using System.IO;
using EtechTaskManagerBackend.Models;

namespace EtechTaskManagerBackend.Services
{
    public class NotificationService
    {
        private readonly string _NotificationlogFilePath = @"C:\Users\Asus\source\repos\EtechTaskManagerBackend\EtechTaskManagerBackend\Services\NotificationLogs.txt";

        public NotificationService()
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_NotificationlogFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public async Task LogNotificationDetails(Notifications notification)
        {
            // Format the log entry
            var logEntry = $"Message: {notification.Message},\n Id e userit: {notification.Recipient},\n " +
                           $"Type: {notification.Type},\n Date: {notification.Date},\n IsRead: {notification.IsRead}\nTimestamp: {DateTime.Now}\n\n";

            try
            {
                // Write the log entry to the file
              await File.AppendAllTextAsync(_NotificationlogFilePath, logEntry);
            }
            catch (Exception ex)
            {
                // Catch any errors that might occur while writing to the file
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }
}
