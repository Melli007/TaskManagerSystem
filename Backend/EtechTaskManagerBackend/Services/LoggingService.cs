using System;
using System.IO;
using EtechTaskManagerBackend.Models;

namespace EtechTaskManagerBackend.Services
{
    public class LoggingService
    {
        private readonly string _logFilePath = @"C:\Users\Asus\source\repos\EtechTaskManagerBackend\EtechTaskManagerBackend\Services\UserLogs.txt";

        public LoggingService()
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void LogUserDetails(Users user)
        {
            // Format the log entry
            var logEntry = $"Id: {user.Id},\n FullName: {user.FullName},\n Username: {user.Username},\n " +
                           $"Email: {user.Email},\n Password: {user.Password},\n Phone: {user.Phone},\n Role: {user.Role},\n Proffesion: {user.Profession},\n CreatedAt: {user.CreatedAt}\nTimestamp: {DateTime.Now},\n Banned: {user.Banned}\n\n";

            try
            {
                // Write the log entry to the file
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                // Catch any errors that might occur while writing to the file
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }
}
