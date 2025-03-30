using EtechTaskManagerBackend.Models;

namespace EtechTaskManagerBackend.Interfaces
{
    public interface IUsersRepository
    {
        ICollection<Users> GetUsers();             // Get all users
        Users GetUserById(int Id);                 // Get user by Id
        Users GetUserByFullName(string fullName);  // Get user by FullName
        Users GetUserByUsername(string username);  // Get user by Username
        ICollection<Users> GetUsersByRole(string role);  // Get users by Role
        bool UserExists(int Id);//Exists Check
        bool CreateUsers(Users users);
        bool UpdateUsers(Users users);
        bool UpdateUsersInfo(Users users);
        bool DeleteUser(Users users);
        bool UpdateUserBannedStatus(int userId, bool isBanned);
        bool UpdateUserOnlineStatus(int userId, bool isOnline);
        bool UpdateProfilePicturePath(int userId, string profilePicturePath);
        string GetProfilePicturePath(int userId);
        bool SetAllUsersOffline();
        bool Save();

        // **Add these for password-reset flow**:
        Users GetUserByEmail(string email);
        bool SaveResetKey(string email, string resetKey, DateTime? expiry);
        Users GetUserByResetKey(string resetKey);
    }
}
