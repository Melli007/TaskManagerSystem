using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace EtechTaskManagerBackend.Repository
{
    public class NotificationsRepository : INotificationsRepository
    {
        private readonly DataContext _context;

        public NotificationsRepository(DataContext context)
        {
            _context = context;
        }

        public ICollection<Notifications> GetNotifications(int userId)
        {
            return _context.Notifications
                .Where(n => n.Recipient == userId)
                .OrderBy(n => n.Date)
                .ToList();
        }

        public Notifications GetNotification(int id)
        {
            return _context.Notifications.FirstOrDefault(n => n.Id == id);
        }

        public int CountUnreadNotifications(int userId)
        {
            return _context.Notifications.Count(n => n.Recipient == userId && !n.IsRead);
        }

        public bool MarkAsRead(int id)
        {
            var notification = GetNotification(id);
            if (notification == null) return false;

            notification.IsRead = true;
            return Save();
        }

        public bool AddNotification(Notifications notification)
        {
            _context.Add(notification);
            return Save();
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false; // Ensure at least one change was saved
        }

        public bool UpdateNotification(Notifications notification)
        {
            _context.Update(notification);
            return Save();
        }

        public bool DeleteNotification(Notifications notification)
        {
           _context.Remove(notification);
            return Save();
        }

        public bool NotificationExists(int id)
        {
            return _context.Notifications.Any(n => n.Id == id);
        }

        public ICollection<Notifications> GetAllNotifications()
        {
            return _context.Notifications
        .OrderBy(n => n.Id)
        .ToList();
        }

        public bool MarkAllAsRead(int userId)
        {
            // Fetch all unread notifications for the specified user
            var unreadNotifications = _context.Notifications
                .Where(n => n.Recipient == userId && !n.IsRead)
                .ToList();

            // Check if there are any unread notifications to update
            if (!unreadNotifications.Any())
            {
                return false; // No notifications to mark as read
            }

            // Mark each notification as read
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            // Save changes to the database
            return Save();
        }

        public bool DeleteAllNotifications(int userId)
        {
            // Fetch all notifications for the specified user
            var userNotifications = _context.Notifications
                .Where(n => n.Recipient == userId)
                .ToList();

            // Check if there are any notifications to delete
            if (!userNotifications.Any())
            {
                return false; // No notifications to delete
            }

            // Remove each notification
            _context.Notifications.RemoveRange(userNotifications);

            // Save changes to the database
            return Save();
        }

        public int GetUnreadNotificationCount(int userId)
        {
            return _context.Notifications
               .Where(n => n.Recipient == userId && !n.IsRead)
               .Count();
        }

        public ICollection<Notifications> GetAllNotificationsForAdmin(string createdBy)
        {
            return _context.Notifications
               .Include(n => n.User) // Ensure User is loaded
               .Where(n => n.User.Role != "Admin" && n.CreatedBy == createdBy)
               .OrderBy(n => n.Id)
               .ToList();
        }
    }
}
