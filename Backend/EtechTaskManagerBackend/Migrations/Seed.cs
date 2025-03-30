using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace EtechTaskManagerBackend.Migrations
{
    public class Seed
    {
        private readonly DataContext _dataContext;

        public Seed(DataContext context)
        {
            _dataContext = context;
        }

        public void SeedDataContext()
        {
            // Seed Users
            if (!_dataContext.Users.Any())
            {
                var users = new List<Users>
                {
                    new Users { FullName = "Melos Devolli", Username = "MelosDevolli", Password = "hashedpassword23", Role = "Admin", CreatedAt = DateTime.UtcNow },
                    new Users { FullName = "Kadri Veseli", Username = "KadriVesli", Password = "KadriHashim2024", Role = "employee", CreatedAt = DateTime.UtcNow },
                    new Users { FullName = "Johnny Sins", Username = "JohnnySins54", Password = "JohnnySins3", Role = "employee", CreatedAt = DateTime.UtcNow },
                    new Users { FullName = "Kushtrim Hasani", Username = "KushtrimDrenicaki", Password = "KushaKusha123", Role = "employee", CreatedAt = DateTime.UtcNow }
                };

                _dataContext.Users.AddRange(users);
                _dataContext.SaveChanges(); // Save after adding users to get their IDs
            }

            // Get user IDs
            int adminUserId = _dataContext.Users.First(u => u.Username == "MelosDevolli").Id;
            int kadriUserId = _dataContext.Users.First(u => u.Username == "KadriVesli").Id;
            int johnnyUserId = _dataContext.Users.First(u => u.Username == "JohnnySins54").Id;
            int kushtrimUserId = _dataContext.Users.First(u => u.Username == "KushtrimDrenicaki").Id;

            // Seed Tasks
            if (!_dataContext.Tasks.Any())
            {
                var tasks = new List<Tasks>
                {
                    new Tasks { Title = "Complete Project", Description = "Finish the backend of the project", AssignedTo = kadriUserId, Status = "in_progress", CreatedAt = DateTime.UtcNow },
                    new Tasks { Title = "Fix Bugs", Description = "Fix all reported bugs", AssignedTo = johnnyUserId, Status = "pending", CreatedAt = DateTime.UtcNow },
                    new Tasks { Title = "Design Frontend", Description = "Create a responsive frontend design", AssignedTo = kushtrimUserId, Status = "in_progress", CreatedAt = DateTime.UtcNow },
                    new Tasks { Title = "Prepare Presentation", Description = "Prepare a project presentation for the team", AssignedTo = adminUserId, Status = "completed", CreatedAt = DateTime.UtcNow }
                };

                _dataContext.Tasks.AddRange(tasks);
            }

            // Seed Notifications
            if (!_dataContext.Notifications.Any())
            {
                var notifications = new List<Notifications>
                {
                    new Notifications { Message = "Task 'Complete Project' assigned to you", Recipient = kadriUserId, Type = "task", Date = DateTime.UtcNow, IsRead = false },
                    new Notifications { Message = "Task 'Fix Bugs' assigned to you", Recipient = johnnyUserId, Type = "task", Date = DateTime.UtcNow, IsRead = false },
                    new Notifications { Message = "Task 'Design Frontend' assigned to you", Recipient = kushtrimUserId, Type = "task", Date = DateTime.UtcNow, IsRead = false },
                    new Notifications { Message = "Task 'Prepare Presentation' completed", Recipient = adminUserId, Type = "reminder", Date = DateTime.UtcNow, IsRead = true },
                    new Notifications { Message = "New task deadline approaching", Recipient = kushtrimUserId, Type = "reminder", Date = DateTime.UtcNow, IsRead = false }
                };

                _dataContext.Notifications.AddRange(notifications);
            }

            // Save all changes to the database
            _dataContext.SaveChanges();
        }
    }
}
