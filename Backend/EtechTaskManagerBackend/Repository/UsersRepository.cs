using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;


namespace EtechTaskManagerBackend.Repository
{
    public class UsersRepository : IUsersRepository
    {
        private readonly DataContext _context;
        public UsersRepository(DataContext context)
        {
                _context = context;
        }
        public ICollection<Users> GetUsers()
        { 
            return _context.Users.OrderBy(u => u.Id).ToList();
        }
        
        public Users GetUserById(int id)
        {
            return _context.Users.Where(u => u.Id == id).FirstOrDefault();
        }

        public Users GetUserByFullName(string fullName)
        {
            return _context.Users.Where(u => u.FullName == fullName).FirstOrDefault();
        }

        public Users GetUserByUsername(string username)
        {
            return _context.Users.Where(u => u.Username == username).FirstOrDefault();
        }

        public ICollection<Users> GetUsersByRole(string role)
        {
            return _context.Users.Where(u => u.Role.ToLower() == role.ToLower()).ToList();
        }

        public bool UserExists(int id)
        {
            return _context.Users.Any(u => u.Id == id);
        }

        public bool CreateUsers(Users users)
        {
            _context.Add(users);
            return Save();
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool UpdateUsers(Users users)
        {
           _context.Update(users);
            return Save();
        }

        public bool DeleteUser(Users users)
        {
            // Delete messages where the user is a recipient
            var recipientMessages = _context.Messages.Where(m => m.RecipientId == users.Id).ToList();
            _context.Messages.RemoveRange(recipientMessages);

            // Optionally, delete messages where the user is a sender
            var senderMessages = _context.Messages.Where(m => m.SenderId == users.Id).ToList();
            _context.Messages.RemoveRange(senderMessages);

            _context.Remove(users);
            return Save();
        }

        public bool UpdateUserBannedStatus(int userId, bool isBanned)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.Banned = isBanned;
            return Save();
        }

        public bool UpdateUserOnlineStatus(int userId, bool isOnline)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.IsOnline = isOnline;
            return Save();
        }

        public bool SetAllUsersOffline()
        {
            var users = _context.Users.Where(u => u.IsOnline).ToList();
            foreach (var user in users)
            {
                user.IsOnline = false; // Mark each user as offline
            }
            return Save();
        }


        public bool UpdateUsersInfo(Users users)
        {
            _context.Update(users);
            return Save();
        }
        public bool UpdateProfilePicturePath(int userId, string profilePicturePath)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.ProfilePicturePath = profilePicturePath;
            return Save();
        }

        public string GetProfilePicturePath(int userId)
        {
            // Retrieve the user from the database
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            // If user doesn't exist or path is null, return null
            return user?.ProfilePicturePath;
        }

        public Users GetUserByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }

        public bool SaveResetKey(string email, string resetKey, DateTime? expiry)
        {
            var user = GetUserByEmail(email);
            if (user == null)
                return false; // user doesn't exist

            user.ResetKey = resetKey;
            user.ResetKeyExpiry = expiry; // e.g. DateTime.Now.AddHours(1);
            return Save();
        }

        public Users GetUserByResetKey(string resetKey)
        {
            return _context.Users.FirstOrDefault(u => u.ResetKey == resetKey);
        }


    }
}
