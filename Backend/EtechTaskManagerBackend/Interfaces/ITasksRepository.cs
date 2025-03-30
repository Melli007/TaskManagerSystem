using EtechTaskManagerBackend.Models;

namespace EtechTaskManagerBackend.Interfaces
{
    public interface ITasksRepository
    {
        ICollection<Tasks> GetAllTasks();  // Retrieves all tasks
        ICollection<Tasks> GetTasks(int userId);
        Tasks GetTask(int id);  // Retrieves a specific task by its ID
        bool CreateTask(Tasks task);  // Creates a new task
        bool Save();  // Persists changes to the database
        bool TaskExists(int id);//Exists Check
        bool UpdateTask(Tasks task);
        bool DeleteTask(Tasks task);
        ICollection<Tasks> GetTasksAssignedToUser(int userId);
        ICollection<Tasks> GetTasksCreatedByUser(int userId);

    }
}
