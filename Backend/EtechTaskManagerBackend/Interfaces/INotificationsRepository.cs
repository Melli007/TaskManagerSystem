using EtechTaskManagerBackend.Models;
using System.Collections.Generic;

namespace EtechTaskManagerBackend.Interfaces
{
    public interface INotificationsRepository
    {
        ICollection<Notifications> GetAllNotifications();
        ICollection<Notifications> GetNotifications(int userId); // Get notifications for a specific user
        ICollection<Notifications> GetAllNotificationsForAdmin(string createdBy);
        Notifications GetNotification(int id); // Get a notification by ID
        int CountUnreadNotifications(int userId);
        bool MarkAsRead(int id); // Mark a notification as read
        bool MarkAllAsRead(int userId); // Marks all notifications for a user as read
        bool Save(); // Save changes to the database
        bool AddNotification(Notifications notification); // Method to add a new notification
        bool UpdateNotification(Notifications notification);
        bool DeleteNotification(Notifications notification);
        bool NotificationExists(int id);//Exists Check
        bool DeleteAllNotifications(int userId); // Delete all notifications for a specific user
        int GetUnreadNotificationCount(int userId);
    }
}
