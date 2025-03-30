using ETechTaskManager.Models;
using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace ETechTaskManager.Controllers
{
    
    public class TasksController1 : Controller
    {
        Uri baseAddress = new Uri("https://localhost:7013/api");

        private readonly HttpClient _client;

        public TasksController1()
        {
            _client = new HttpClient();
            _client.BaseAddress = baseAddress;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string status = null,        // e.g. "Të Përfunduara", "Të Vonuara", etc.
    string dueDate = null,       // e.g. "null" for Pa Deadline
    string sortOrder = null,     // "asc" or "desc"
    DateTime? startDate = null,
    DateTime? endDate = null,
    bool highPriority = false,   // optional
    bool weeklyCreated = false // new checkbox for tasks created this week)
            )
        {
            List<TasksViewModel> tasksList = new List<TasksViewModel>();
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            // Fetch tasks from the backend
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetAllTasks");

            // Fetch users for mapping purposes
            HttpResponseMessage usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");

            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = await usersResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            if (tasksResponse.IsSuccessStatusCode)
            {
                string data = await tasksResponse.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(data);

                foreach (var task in tasksList)
                {
                    // Map AssignedTo ID to FullName for display
                    var assignedUser = usersList.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    var CreatedTaskBy = usersList.FirstOrDefault(u =>u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";


                    // Check for overdue tasks and update status if necessary
                    if (task.Status != "Të Përfunduara" && task.DueDate < DateTime.Now)
                    {
                        if (task.Status != "Të Vonuara")
                        {
                            task.Status = "Të Vonuara";

                        }
                    }

                    // Serialize and send updated task to the backend
                    string updatedTaskData = JsonConvert.SerializeObject(task);
                    StringContent content = new StringContent(updatedTaskData, Encoding.UTF8, "application/json");

                    // Send the PUT request to update the task status in the backend
                    await _client.PutAsync($"{_client.BaseAddress}/Tasks/UpdateTask/{task.Id}", content);
                }
                // 4) Apply filters:
                //    a) Pa Deadline => dueDate = "null"
                if (!string.IsNullOrEmpty(dueDate) && dueDate == "null")
                {
                    tasksList = tasksList.Where(t => t.DueDate == null).ToList();
                    ViewBag.Status = "Pa Deadline";
                }
                //    b) status => "Të Përfunduara", "Të Vonuara", etc.
                else if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Afati Sot")
                    {
                        // Only tasks due today
                        tasksList = tasksList.Where(t => t.DueDate?.Date == DateTime.Today).ToList();
                        ViewBag.Status = "Afati Sot";
                    }
                    else
                    {
                        tasksList = tasksList.Where(t => t.Status == status).ToList();
                        ViewBag.Status = status;
                    }
                }
                else
                {
                    ViewBag.Status = "Menagjo Detyrat"; // default heading
                }

                //    c) Filter by creation date range (startDate, endDate)
                if (startDate.HasValue)
                {
                    tasksList = tasksList.Where(t => t.CreatedAt >= startDate.Value).ToList();
                }
                if (endDate.HasValue)
                {
                    tasksList = tasksList.Where(t => t.CreatedAt <= endDate.Value).ToList();
                }

                //    d) Sort by CreatedAt
                if (sortOrder == "asc")
                {
                    tasksList = tasksList.OrderBy(t => t.CreatedAt).ToList();
                }
                else if (sortOrder == "desc")
                {
                    tasksList = tasksList.OrderByDescending(t => t.CreatedAt).ToList();
                }

                //    e) High Priority
                if (highPriority)
                {
                    tasksList = tasksList.Where(t =>
                    {
                        // skip tasks that are completed or overdue
                        if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                            return false;

                        // skip tasks with no DueDate
                        if (!t.DueDate.HasValue)
                            return false;

                        // check if >= 50% elapsed
                        TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                        TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;
                        return elapsedTime >= totalTime / 2;
                    })
                    .ToList();
                }

                //    f) Weekly Created (new)
                //       Only tasks created in the last 7 days
                if (weeklyCreated)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt >= DateTime.Now.AddDays(-7))
                        .ToList();
                }
            }

            // 5) Return the filtered tasks
            return View(tasksList);
        }

        [HttpGet]
        public async Task<IActionResult> GetTasksByUserId(
    string status,
    string sortOrder,    // "asc" or "desc"
    DateTime? startDate, // optional start date
    DateTime? endDate,    // optional end date
    bool highPriority = false
)
        {
            // Retrieve the current user's ID from the session
            string username = HttpContext.Session.GetString("Username");

            if (username == null)
            {
                return RedirectToAction("Login", "Home"); // Redirect to login if user ID is not found
            }

            // Fetch the user to get the ID based on the username
            HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            List<UsersDTO> users = new List<UsersDTO>();

            if (userResponse.IsSuccessStatusCode)
            {
                string userData = await userResponse.Content.ReadAsStringAsync();
                users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);
            }

            // Find the current user's ID
            var currentUser = users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Fetch tasks for the specific user
            List<TasksViewModel> tasksList = new List<TasksViewModel>();
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasks/user/{currentUser.Id}");

            if (tasksResponse.IsSuccessStatusCode)
            {
                string data = await tasksResponse.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(data);

                // Map AssignedTo ID to FullName and update task status
                foreach (var task in tasksList)
                {
                    // Map AssignedTo ID to FullName for display
                    var assignedUser = users.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    var CreatedTaskBy = users.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";

                    // Check for overdue tasks and update status if necessary
                    if (task.Status != "Të Përfunduara" && task.DueDate < DateTime.Now)
                    {
                        if (task.Status != "Të Vonuara")
                        {
                            task.Status = "Të Vonuara";
                        }
                    }
                }
                // a) Filter by Status
                if (!string.IsNullOrEmpty(status))
                {
                    tasksList = tasksList
                        .Where(t => t.Status == status)
                        .ToList();
                }

                // b) Filter by date range (example: CreatedAt)
                //    If you want to filter by DueDate instead, swap the property below.
                if (startDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt <= endDate.Value)
                        .ToList();
                }

                // c) Sort by CreatedAt date (ascending or descending)
                if (sortOrder == "asc")
                {
                    tasksList = tasksList
                        .OrderBy(t => t.CreatedAt)
                        .ToList();
                }
                else if (sortOrder == "desc")
                {
                    tasksList = tasksList
                        .OrderByDescending(t => t.CreatedAt)
                        .ToList();
                }
                // d) Filter by high priority (50% of time elapsed)
                if (highPriority)
                {
                    tasksList = tasksList
                        .Where(t =>
                        {
                            // Skip tasks with status "Të Përfunduara" or "Të Vonuara"
                            if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                            {
                                return false;
                            }

                            // Skip tasks without a deadline (DueDate is null)
                            if (t.DueDate == null)
                            {
                                return false;
                            }

                            // Calculate total time and elapsed time
                            TimeSpan totalTime = t.DueDate.Value - t.CreatedAt; // Total time between creation and deadline
                            TimeSpan elapsedTime = DateTime.Now - t.CreatedAt; // Time elapsed since creation

                            // Check if 50% of the time has passed
                            return elapsedTime >= totalTime / 2;
                        })
                        .ToList();
                }
            }
            else
            {
                TempData["errorMessage"] = "Failed to fetch tasks.";
            }


            return View(tasksList);
        }

[HttpGet]
public async Task<IActionResult> GetAssignedTasks(
        string status,
        string sortOrder,
        DateTime? startDate,
        DateTime? endDate,
        bool highPriority = false
        )
        {
    string username = HttpContext.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return RedirectToAction("Login", "Home"); // Redirect if the user is not logged in
    }

    // Fetch the user's ID
    HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
    List<UsersViewModel> usersList = new List<UsersViewModel>();
    if (userResponse.IsSuccessStatusCode)
    {
        string usersData = await userResponse.Content.ReadAsStringAsync();
        usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
    }

    var currentUser = usersList.FirstOrDefault(u => u.Username == username);
    if (currentUser == null)
    {
        return RedirectToAction("Login", "Home");
    }

            // Fetch tasks assigned to the user
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasksAssignedToUser/assignedtouser/{currentUser.Id}");
          List<TasksViewModel> tasksList = new List<TasksViewModel>();

            if (tasksResponse.IsSuccessStatusCode)
            {
              string tasksData = await tasksResponse.Content.ReadAsStringAsync();
              tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(tasksData);
                
                // Map
                foreach (var task in tasksList)
                {
                    var CreatedTaskBy = usersList.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";
                }

                // a) Filter by Status
                if (!string.IsNullOrEmpty(status))
                {
                    tasksList = tasksList
                        .Where(t => t.Status == status)
                        .ToList();
                }

                // b) Filter by date range (CreatedAt used here, or DueDate if you prefer)
                if (startDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt <= endDate.Value)
                        .ToList();
                }

                // c) Sort by CreatedAt (ascending or descending)
                if (sortOrder == "asc")
                {
                    tasksList = tasksList
                        .OrderBy(t => t.CreatedAt)
                        .ToList();
                }
                else if (sortOrder == "desc")
                {
                    tasksList = tasksList
                        .OrderByDescending(t => t.CreatedAt)
                        .ToList();
                }

                // d) Filter by "High Priority" (50% time elapsed, ignoring completed/overdue tasks)
                if (highPriority)
                {
                    tasksList = tasksList
                        .Where(t =>
                        {
                            // skip tasks that are completed or overdue
                            if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                                return false;

                            // skip tasks with no DueDate
                            if (!t.DueDate.HasValue)
                                return false;

                            TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                            TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;
                            // Only keep tasks at or beyond 50% elapsed time
                            return elapsedTime >= (totalTime / 2);
                        })
                        .ToList();
                }
            }

    // Show a message if no tasks are assigned
    if (tasksList == null || !tasksList.Any())
    {
        ViewBag.Message = "You have no tasks assigned.";
    }

    return View(tasksList);
}

        [HttpGet]
        public async Task<IActionResult> GetCreatedTasks(
        string status,
        string sortOrder,
        DateTime? startDate,
        DateTime? endDate,
        bool highPriority = false
        )
        {
            string username = HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Home"); // Redirect if the user is not logged in
            }

            // Fetch the user's ID
            HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            List<UsersViewModel> usersList = new List<UsersViewModel>();
            if (userResponse.IsSuccessStatusCode)
            {
                string usersData = await userResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            var currentUser = usersList.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Fetch tasks created by the user
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasksCreatedByUser/createdByUser/{currentUser.Id}");
            List<TasksViewModel> tasksList = new List<TasksViewModel>();

            if (tasksResponse.IsSuccessStatusCode)
            {
                string tasksData = await tasksResponse.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(tasksData);

                // Map
                foreach (var task in tasksList)
                {
                    var CreatedTaskBy = usersList.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";
                }

                // a) Filter by Status
                if (!string.IsNullOrEmpty(status))
                {
                    tasksList = tasksList
                        .Where(t => t.Status == status)
                        .ToList();
                }

                // b) Filter by date range (CreatedAt used here, or DueDate if you prefer)
                if (startDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt <= endDate.Value)
                        .ToList();
                }

                // c) Sort by CreatedAt (ascending or descending)
                if (sortOrder == "asc")
                {
                    tasksList = tasksList
                        .OrderBy(t => t.CreatedAt)
                        .ToList();
                }
                else if (sortOrder == "desc")
                {
                    tasksList = tasksList
                        .OrderByDescending(t => t.CreatedAt)
                        .ToList();
                }

                // d) Filter by "High Priority" (50% time elapsed, ignoring completed/overdue tasks)
                if (highPriority)
                {
                    tasksList = tasksList
                        .Where(t =>
                        {
                            // skip tasks that are completed or overdue
                            if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                                return false;

                            // skip tasks with no DueDate
                            if (!t.DueDate.HasValue)
                                return false;

                            TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                            TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;
                            // Only keep tasks at or beyond 50% elapsed time
                            return elapsedTime >= (totalTime / 2);
                        })
                        .ToList();
                }

            }

            return View(tasksList); // Use GetCreatedTasks.cshtml
        }

        [HttpGet]
        public async Task<IActionResult> GetTasksByProfession(
        string status,
        string sortOrder,
        DateTime? startDate,
        DateTime? endDate,
        bool highPriority = false
        )
        {
            string username = HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Home"); // Redirect if the user is not logged in
            }

            // Fetch the current user by username
            HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            if (userResponse.IsSuccessStatusCode)
            {
                string usersData = await userResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            var currentUser = usersList.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Call the backend API to fetch tasks by profession
            HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasksByProfession/{currentUser.Profession}");
            List<TasksViewModel> tasksList = new List<TasksViewModel>();

            if (response.IsSuccessStatusCode)
            {
                string tasksData = await response.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(tasksData);

                // Map
                foreach (var task in tasksList)
                {
                    var assignedUser = usersList.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    var createdByUser = usersList.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = createdByUser != null ? createdByUser.FullName : "Admin";
                }

                if (tasksList == null || !tasksList.Any())
                {
                    TempData["infoMessage"] = "No tasks found for your profession.";
                }

                // a) Filter by Status
                if (!string.IsNullOrEmpty(status))
                {
                    tasksList = tasksList
                        .Where(t => t.Status == status)
                        .ToList();
                }

                // b) Filter by date range (CreatedAt used here, or DueDate if you prefer)
                if (startDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    tasksList = tasksList
                        .Where(t => t.CreatedAt <= endDate.Value)
                        .ToList();
                }

                // c) Sort by CreatedAt (ascending or descending)
                if (sortOrder == "asc")
                {
                    tasksList = tasksList
                        .OrderBy(t => t.CreatedAt)
                        .ToList();
                }
                else if (sortOrder == "desc")
                {
                    tasksList = tasksList
                        .OrderByDescending(t => t.CreatedAt)
                        .ToList();
                }

                // d) Filter by "High Priority" (50% time elapsed, ignoring completed/overdue tasks)
                if (highPriority)
                {
                    tasksList = tasksList
                        .Where(t =>
                        {
                            // skip tasks that are completed or overdue
                            if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                                return false;

                            // skip tasks with no DueDate
                            if (!t.DueDate.HasValue)
                                return false;

                            TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                            TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;
                            // Only keep tasks at or beyond 50% elapsed time
                            return elapsedTime >= (totalTime / 2);
                        })
                        .ToList();
                }

                return View(tasksList);
            }
            else
            {
                TempData["errorMessage"] = $"Failed to fetch tasks for profession: {currentUser.Profession}.";
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            string userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userRole))
                return RedirectToAction("Login", "Home");

            var usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            if (usersResponse.IsSuccessStatusCode)
            {
                string data = await usersResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(data);
            }

            var model = new TasksViewModel
            {
                Users = usersList
            };

            // Redirect users to "CreateMyTasks" view
            if (userRole == "User")
                return RedirectToAction("CreateMyTasks");

            return View(model); // Default admin view
        }

        [HttpPost]
        public async Task<IActionResult> Create(TasksViewModel model, [FromForm] string assignedUsers)
        {
            try
            {
                // 1) Verify the current admin/creator
                string userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                    return RedirectToAction("Login", "Home");

                model.CreatedBy = currentUserId;

                var userIdList = new List<int>();

                if (!string.IsNullOrWhiteSpace(assignedUsers))
                {
                    // Split on commas
                    userIdList = assignedUsers
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(idString => int.Parse(idString.Trim()))
                        .ToList();
                }

                // 2) If the user selected "Të gjithve" => assignedUsers = [-1], meaning "assign to all"
                if (userIdList.Count == 1 && userIdList[0] == -1)
                {
                    // *** Assign to all users ***
                    var usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                    if (!usersResponse.IsSuccessStatusCode)
                    {
                        TempData["errorMessage"] = "Failed to fetch users for assignment.";
                        return RedirectToAction("Index");
                    }

                    var usersData = await usersResponse.Content.ReadAsStringAsync();
                    var allUsers = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);

                    foreach (var u in allUsers)
                    {
                        await CreateTaskForUserAsync(model, u.Id);
                    }

                    TempData["successMessage"] = "Task successfully assigned to ALL users.";
                }
                // 3) If the user selected multiple IDs (but not -1)
                else if (userIdList.Count > 1)
                {
                    // *** Assign to multiple users ***
                    foreach (var userId in userIdList)
                    {
                        await CreateTaskForUserAsync(model, userId);
                    }

                    TempData["successMessage"] = "Task assigned to the selected users.";
                }
                // 4) If there's exactly one user ID (and it's not -1)
                else if (userIdList.Count == 1 && userIdList[0] != -1)
                {
                    // *** Single user assignment ***
                    int singleUser = userIdList[0];
                    await CreateTaskForUserAsync(model, singleUser);

                    TempData["successMessage"] = "Task created for the single selected user.";
                }
                else
                {
                    TempData["errorMessage"] = "No user(s) selected.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                TempData["errorMessage"] = $"An error occurred: {ex.Message}";
                return View(model);
            }
        }

        private async Task CreateTaskForUserAsync(TasksViewModel model, int userId)
        {
            using (var formData = new MultipartFormDataContent())
            {
                // Title, Description, Status
                formData.Add(new StringContent(model.Title), "Title");
                formData.Add(new StringContent(model.Description), "Description");
                formData.Add(new StringContent(model.Status), "Status");

                // AssignedTo => the userId we're looping on
                formData.Add(new StringContent(userId.ToString()), "AssignedTo");

                // CreatedBy => the admin
                formData.Add(new StringContent(model.CreatedBy.ToString()), "CreatedBy");

                // Optional: DueDate
                if (model.DueDate.HasValue)
                {
                    formData.Add(new StringContent(model.DueDate.Value.ToString("o")), "DueDate");
                }

                // Optional: File
                if (model.File != null && model.File.Length > 0)
                {
                    var fileContent = new StreamContent(model.File.OpenReadStream());
                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(model.File.ContentType);
                    formData.Add(fileContent, "File", model.File.FileName);
                }

                var response = await _client.PostAsync($"{_client.BaseAddress}/Tasks/CreateTask", formData);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to assign task to user {userId}: {errorMsg}");
                    // Optionally store an error message.
                }
            }
        }


        [HttpGet]
        public async Task<IActionResult> CreateMyTasks()
        {
            string username = HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Home");

            var usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            if (usersResponse.IsSuccessStatusCode)
            {
                string data = await usersResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(data);
            }

            var model = new TasksViewModel
            {
                Users = usersList
            };

            return View(model); // Return user-specific task creation view
        }

        [HttpPost]
        public async Task<IActionResult> CreateMyTasks(TasksViewModel model)
        {
            try
            {
                string username = HttpContext.Session.GetString("Username");
                string userIdStr = HttpContext.Session.GetString("UserId");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                {
                    return RedirectToAction("Login", "Home");
                }

                // Fetch current user from API
                HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                if (userResponse.IsSuccessStatusCode)
                {
                    string usersData = await userResponse.Content.ReadAsStringAsync();
                    var usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
                    var currentUser = usersList.FirstOrDefault(u => u.Username == username);

                    if (currentUser != null)
                    {
                        model.AssignedTo = currentUser.Id;
                        model.CreatedBy = currentUserId; // Set CreatedBy using the UserId from session

                        using (var formData = new MultipartFormDataContent())
                        {
                            formData.Add(new StringContent(model.Title, Encoding.UTF8, MediaTypeNames.Text.Plain), "Title");
                            formData.Add(new StringContent(model.Description, Encoding.UTF8, MediaTypeNames.Text.Plain), "Description");
                            formData.Add(new StringContent(model.Status, Encoding.UTF8, MediaTypeNames.Text.Plain), "Status");
                            formData.Add(new StringContent(model.AssignedTo.ToString(), Encoding.UTF8, MediaTypeNames.Text.Plain), "AssignedTo");
                            formData.Add(new StringContent(model.CreatedBy.ToString(), Encoding.UTF8, MediaTypeNames.Text.Plain), "CreatedBy");

                            if (model.DueDate.HasValue)
                                formData.Add(new StringContent(model.DueDate.Value.ToString("o"), Encoding.UTF8, MediaTypeNames.Text.Plain), "DueDate");

                            if (model.File != null && model.File.Length > 0)
                            {
                                var fileContent = new StreamContent(model.File.OpenReadStream());
                                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.File.ContentType);
                                formData.Add(fileContent, "File", model.File.FileName);
                            }

                            var response = await _client.PostAsync($"{_client.BaseAddress}/Tasks/CreateTask", formData);
                            if (response.IsSuccessStatusCode)
                            {
                                TempData["successMessage"] = "Task created successfully.";
                                return RedirectToAction("GetCreatedTasks");
                            }
                            else
                            {
                                string errorMsg = await response.Content.ReadAsStringAsync();
                                TempData["errorMessage"] = $"Failed to create task: {errorMsg}";
                                return View(model);
                            }
                        }
                    }
                }

                TempData["errorMessage"] = "Failed to fetch user details.";
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"An error occurred: {ex.Message}";
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> DownloadFile(int taskId)
        {
            try
            {
                // Call the API endpoint to download the file
                var response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/DownloadFile/download/{taskId}");

                if (response.IsSuccessStatusCode)
                {
                    // Get the file bytes
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();

                    // Extract the file name from the Content-Disposition header
                    if (response.Content.Headers.ContentDisposition != null)
                    {
                        var contentDisposition = response.Content.Headers.ContentDisposition;
                        var fileName = contentDisposition.FileNameStar ?? contentDisposition.FileName ?? "downloaded_file";

                        // Return the file to the client
                        return File(fileBytes, response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", fileName);
                    }
                    else
                    {
                        // Fallback if Content-Disposition header is not present
                        return File(fileBytes, "application/octet-stream", "downloaded_file");
                    }
                }
                else
                {
                    TempData["errorMessage"] = "Failed to download the file. The file may not exist.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"An error occurred while downloading the file: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, string returnUrl = null)
        { 
            try
            {

                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = Request.Headers["Referer"].ToString(); // Use the referring page as the fallback
                }

                TasksViewModel task = new TasksViewModel();
                HttpResponseMessage response = _client.GetAsync(_client.BaseAddress + "/Tasks/GetTask/" + id).Result;

                if (response.IsSuccessStatusCode)
                {
                    string data = response.Content.ReadAsStringAsync().Result;
                    task = JsonConvert.DeserializeObject<TasksViewModel>(data);
                }
                else
                {
                    TempData["errorMessage"] = "Failed to retrieve the task.";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }

                // Fetch all users for the dropdown
                HttpResponseMessage usersResponse = await _client.GetAsync(_client.BaseAddress + "/Users/GetUsers");
                if (usersResponse.IsSuccessStatusCode)
                {
                    string usersData = await usersResponse.Content.ReadAsStringAsync();
                    task.Users = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
                }

                // Pass the returnUrl to the view
                ViewBag.ReturnUrl = returnUrl;

                return View(task);

            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return Redirect(returnUrl ?? Url.Action("Index"));
            } 
        }

        [HttpPost]
        public async Task<IActionResult> Edit(TasksViewModel model, string returnUrl = null)
        {
            try
            {

                using (var formData = new MultipartFormDataContent())
                {
                    formData.Add(new StringContent(model.Title ?? "", Encoding.UTF8, MediaTypeNames.Text.Plain), "Title");
                    formData.Add(new StringContent(model.Description ?? "", Encoding.UTF8, MediaTypeNames.Text.Plain), "Description");
                    formData.Add(new StringContent(model.Status ?? "", Encoding.UTF8, MediaTypeNames.Text.Plain), "Status");

                    if (model.AssignedTo.HasValue)
                    {
                        formData.Add(new StringContent(model.AssignedTo.Value.ToString(), Encoding.UTF8, MediaTypeNames.Text.Plain), "AssignedTo");
                    }

                    if (model.DueDate.HasValue)
                    {
                        formData.Add(new StringContent(model.DueDate.Value.ToString("o"), Encoding.UTF8, MediaTypeNames.Text.Plain), "DueDate");
                    }

                    if (model.File != null && model.File.Length > 0)
                    {
                        var fileContent = new StreamContent(model.File.OpenReadStream());
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.File.ContentType);
                        formData.Add(fileContent, "File", model.File.FileName);
                    }

                    var response = await _client.PutAsync($"{_client.BaseAddress}/Tasks/UpdateTask/{model.Id}", formData);


                    if (response.IsSuccessStatusCode)
                    {
                        TempData["successMessage"] = "Task details updated.";
                        return Redirect(returnUrl ?? Url.Action("Index"));
                    }
                    else
                    {
                        string errorMsg = await response.Content.ReadAsStringAsync();
                        TempData["errorMessage"] = $"Failed to update task: {errorMsg}";
                    }
                }
            }
            catch (Exception ex)
            {

                TempData["errorMessage"] = ex.Message;
            }

            // Fetch users again if update fails
            var usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = await usersResponse.Content.ReadAsStringAsync();
                model.Users = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            // Return to the edit view with the returnUrl preserved
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditMyTasks(int id, string returnUrl = null) 
        {
            string userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
            {
                return RedirectToAction("Login", "Home");
            }

            // Fetch the task by ID
            TasksViewModel task = new TasksViewModel();
            HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{id}");

            if (response.IsSuccessStatusCode)
            {
                string data = await response.Content.ReadAsStringAsync();
                task = JsonConvert.DeserializeObject<TasksViewModel>(data);

                // Ensure the task belongs to the current user
                if (task.CreatedBy != currentUserId)
                {
                    TempData["errorMessage"] = "You are not authorized to edit this task.";
                    return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                }
                ViewBag.ReturnUrl = returnUrl;
            }

            // Fetch all users for dropdown (if needed)
            HttpResponseMessage usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = await usersResponse.Content.ReadAsStringAsync();
                task.Users = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            return View(task); // Return the edit view
        }

        [HttpPost]
        public async Task<IActionResult> EditMyTasks(TasksViewModel model, string returnUrl = null)
        {
            try
            {
                string userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                {
                    return RedirectToAction("Login", "Home");
                }

                // Ensure the task belongs to the current user
                HttpResponseMessage taskResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{model.Id}");
                if (taskResponse.IsSuccessStatusCode)
                {
                    string taskData = await taskResponse.Content.ReadAsStringAsync();
                    var existingTask = JsonConvert.DeserializeObject<TasksViewModel>(taskData);

                    if (existingTask.CreatedBy != currentUserId)
                    {
                        TempData["errorMessage"] = "You are not authorized to edit this task.";
                        return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                    }
                }

                // Set AssignedTo to the current user's ID
                model.AssignedTo = currentUserId;

                using (var formData = new MultipartFormDataContent())
                {
                    formData.Add(new StringContent(model.Title ?? "", Encoding.UTF8), "Title");
                    formData.Add(new StringContent(model.Description ?? "", Encoding.UTF8), "Description");
                    formData.Add(new StringContent(model.Status ?? "", Encoding.UTF8), "Status");
                    formData.Add(new StringContent(model.AssignedTo.ToString(), Encoding.UTF8), "AssignedTo");

                    if (model.DueDate.HasValue)
                    {
                        formData.Add(new StringContent(model.DueDate.Value.ToString("o"), Encoding.UTF8), "DueDate");
                    }

                    if (model.File != null && model.File.Length > 0)
                    {
                        var fileContent = new StreamContent(model.File.OpenReadStream());
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.File.ContentType);
                        formData.Add(fileContent, "File", model.File.FileName);
                    }

                    var response = await _client.PutAsync($"{_client.BaseAddress}/Tasks/UpdateTask/{model.Id}", formData);

                    if (response.IsSuccessStatusCode)
                    {
                        TempData["successMessage"] = "Task updated successfully.";
                        return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                    }
                    else
                    {
                        string errorMsg = await response.Content.ReadAsStringAsync();
                        TempData["errorMessage"] = $"Failed to update task: {errorMsg}";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"An error occurred: {ex.Message}";
            }

            return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id, string returnUrl = null)
        {
            try
            {
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = Request.Headers["Referer"].ToString(); // Use the referring page as the fallback
                }

                // Fetch the task by ID
                TasksViewModel task = new TasksViewModel();
                HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{id}");

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    task = JsonConvert.DeserializeObject<TasksViewModel>(data);

                    // Fetch all users
                    HttpResponseMessage usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                    if (usersResponse.IsSuccessStatusCode)
                    {
                        string usersData = await usersResponse.Content.ReadAsStringAsync();
                        var usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);

                        // Map AssignedTo ID to FullName
                        var assignedUser = usersList.FirstOrDefault(u => u.Id == task.AssignedTo);
                        task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";
                    }
                }
                else
                {
                    TempData["errorMessage"] = $"Failed to fetch the task with ID: {id}";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }

                // Pass returnUrl to the view
                ViewBag.ReturnUrl = returnUrl;

                return View(task);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return Redirect(returnUrl ?? Url.Action("Index"));
            }
        }


        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id, string returnUrl = null)
        {
            try
            {
                HttpResponseMessage response = await _client.DeleteAsync($"{_client.BaseAddress}/Tasks/DeleteTask/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Task deleted successfully.";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }
                else
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["errorMessage"] = $"Failed to delete task: {errorMsg}";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
            }

            return Redirect(returnUrl ?? Url.Action("Index"));
        }


        [HttpGet]
        public async Task<IActionResult> DeleteMyTasks(int id, string returnUrl = null)
        {
            string userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
            {
                return RedirectToAction("Login", "Home");
            }

            // Fetch the task by ID
            TasksViewModel task = new TasksViewModel();
            HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{id}");

            if (response.IsSuccessStatusCode)
            {
                string data = await response.Content.ReadAsStringAsync();
                task = JsonConvert.DeserializeObject<TasksViewModel>(data);

                // Ensure the task belongs to the current user
                if (task.CreatedBy != currentUserId)
                {
                    TempData["errorMessage"] = "You are not authorized to delete this task.";
                    return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                }
                // Fetch all users
                HttpResponseMessage userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                List<UsersDTO> users = new List<UsersDTO>();

                if (userResponse.IsSuccessStatusCode)
                {
                    string userData = await userResponse.Content.ReadAsStringAsync();
                    users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);

                    // Map AssignedTo ID to FullName
                    var assignedUser = users.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    // Map CreatedBy ID to FullName
                    var createdByUser = users.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = createdByUser != null ? createdByUser.FullName : "Unassigned";
                }
                // Pass the returnUrl to the view via ViewBag
                ViewBag.ReturnUrl = returnUrl;
            }
            else
            {
                TempData["errorMessage"] = $"Failed to fetch the task with ID: {id}";
                return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
            }
            return View(task); // Return the confirmation view
        }


        [HttpPost, ActionName("DeleteMyTasks")]
        public async Task<IActionResult> DeleteMyTasksConfirmed(int id, string returnUrl = null)
        {
            string userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                // Ensure the task belongs to the current user
                HttpResponseMessage taskResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{id}");
                if (taskResponse.IsSuccessStatusCode)
                {
                    string taskData = await taskResponse.Content.ReadAsStringAsync();
                    var existingTask = JsonConvert.DeserializeObject<TasksViewModel>(taskData);

                    if (existingTask.CreatedBy != currentUserId)
                    {
                        TempData["errorMessage"] = "You are not authorized to delete this task.";
                        return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                    }
                }

                HttpResponseMessage response = await _client.DeleteAsync($"{_client.BaseAddress}/Tasks/DeleteTask/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Task deleted successfully.";
                     return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
                }
                else
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["errorMessage"] = $"Failed to delete task: {errorMsg}";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"An error occurred: {ex.Message}";
            }

            return Redirect(returnUrl ?? Url.Action("GetCreatedTasks"));
        }

        [HttpGet]
        public async Task<IActionResult> Statuset(string status = null,
    string dueDate = null,
    string sortOrder = null,     // "asc" or "desc"
    DateTime? startDate = null,  // optional start date
    DateTime? endDate = null,    // optional end date
    bool highPriority = false,   // optionally filter "High Priority"
    bool weeklyCompleted = false)   // optionally filter tasks created in last 7 days)
        {
            var tasksList = await GetTasks();

            // Filter tasks with no deadline if dueDate is "null"
            if (!string.IsNullOrEmpty(dueDate) && dueDate == "null")
            {
                tasksList = tasksList.Where(t => t.DueDate == null).ToList();
                ViewBag.Status = "Pa Deadline"; // Explicitly set heading for "No Deadline"
            }
            // Otherwise, filter tasks by status if provided
            else if (!string.IsNullOrEmpty(status))
            {
                if (status == "Afati Sot")
                {
                    // Filter tasks that are due today
                    tasksList = tasksList.Where(t => t.DueDate?.Date == DateTime.Today).ToList();
                    ViewBag.Status = "Për Afatin e Sotëm"; // Explicitly set heading for "No Deadline"
                }
                else
                {
                    // Filter tasks based on the provided status
                    tasksList = tasksList.Where(t => t.Status == status).ToList();
                    ViewBag.Status = status; // Explicitly set heading for "No Deadline"
                }
            }
            else
            {
                ViewBag.Status = "Filtrimi i Detyrave"; // Default heading
            }

            // (a) Filter by date range (CreatedAt)
            if (startDate.HasValue)
                tasksList = tasksList.Where(t => t.CreatedAt >= startDate.Value).ToList();

            if (endDate.HasValue)
                tasksList = tasksList.Where(t => t.CreatedAt <= endDate.Value).ToList();

            // (b) Sort by CreatedAt (asc/desc)
            if (sortOrder == "asc")
                tasksList = tasksList.OrderBy(t => t.CreatedAt).ToList();
            else if (sortOrder == "desc")
                tasksList = tasksList.OrderByDescending(t => t.CreatedAt).ToList();

            // (c) Filter by High Priority (optional)
            if (highPriority)
            {
                tasksList = tasksList
                    .Where(t =>
                    {
                        // Skip tasks that are completed or overdue
                        if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                            return false;

                        // Skip tasks with no DueDate
                        if (!t.DueDate.HasValue)
                            return false;

                        // Check if >= 50% of the time has elapsed
                        TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                        TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;
                        return elapsedTime >= totalTime / 2;
                    })
                    .ToList();
            }

            // (d) Weekly Created (optional)
            if (weeklyCompleted)
            {
                tasksList = tasksList
                    .Where(t => t.Status == "Të Përfunduara"
                             && t.CreatedAt >= DateTime.Now.AddDays(-7))
                    .ToList();
            }

            return View(tasksList);
        }

        [HttpGet]
        public async Task<IActionResult> FilterStatusByUsers(
    string status = null,
    string dueDate = null,
    string sortOrder = null,     // "asc" or "desc"
    DateTime? startDate = null,  // optional start date
    DateTime? endDate = null,    // optional end date
    bool highPriority = false,  // <<-- new param// high priority filter
     bool weeklyCompleted = false  
)
        {
            // 1. Retrieve the tasks
            var tasksList = await GetTasksByTheUserId();

            //    (Assuming this returns a List<TasksViewModel> or similar)

            // 2. Keep your existing special filters
            //    -------------------------------------------------------
            //    Filter tasks with no deadline if dueDate == "null"
            if (!string.IsNullOrEmpty(dueDate) && dueDate == "null")
            {
                tasksList = tasksList.Where(t => t.DueDate == null).ToList();
                ViewBag.Status = "Pa Deadline";
            }
            // Otherwise, filter tasks by status if provided
            else if (!string.IsNullOrEmpty(status))
            {
                if (status == "Afati Sot")
                {
                    // Filter tasks that are due today
                    tasksList = tasksList.Where(t => t.DueDate?.Date == DateTime.Today).ToList();
                    ViewBag.Status = "Afati Sot";
                }
                else
                {
                    // Filter tasks based on the provided status
                    tasksList = tasksList.Where(t => t.Status == status).ToList();
                    ViewBag.Status = status;
                }
            }
            else
            {
                // Default heading if no specific filter is applied
                ViewBag.Status = "Filtrimi i Detyrave";
            }

            // 3. Add the same (or similar) advanced filters you use elsewhere
            //    ------------------------------------------------------------
            // (a) Filter by date range (CreatedAt, or change to DueDate if needed)
            if (startDate.HasValue)
            {
                tasksList = tasksList.Where(t => t.CreatedAt >= startDate.Value).ToList();
            }

            if (endDate.HasValue)
            {
                tasksList = tasksList.Where(t => t.CreatedAt <= endDate.Value).ToList();
            }

            // (b) Sort by CreatedAt (asc or desc)
            if (sortOrder == "asc")
            {
                tasksList = tasksList.OrderBy(t => t.CreatedAt).ToList();
            }
            else if (sortOrder == "desc")
            {
                tasksList = tasksList.OrderByDescending(t => t.CreatedAt).ToList();
            }

            // (c) Filter by high priority
            //     (50% of time elapsed, ignoring completed/overdue tasks, etc.)
            if (highPriority)
            {
                tasksList = tasksList
                    .Where(t =>
                    {
                        // Skip tasks with status "Të Përfunduara" or "Të Vonuara"
                        if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                            return false;

                        // Skip tasks without a deadline
                        if (!t.DueDate.HasValue)
                            return false;

                        // Calculate total time and elapsed time
                        TimeSpan totalTime = t.DueDate.Value - t.CreatedAt;
                        TimeSpan elapsedTime = DateTime.Now - t.CreatedAt;

                        // Check if 50% of the time has passed
                        return elapsedTime >= totalTime / 2;
                    })
                    .ToList();
            }

            if (weeklyCompleted)
            {
                tasksList = tasksList
                    .Where(t => t.Status == "Të Përfunduara"
                             && t.CreatedAt >= DateTime.Now.AddDays(-7))
                    .ToList();
            }

            // 4. Return the filtered list to the view
            return View(tasksList);
        }

        [HttpPost("ChangeStatus/{id}")]
        public async Task<IActionResult> ChangeStatus(int id )
        {
            try
            {
                // Check if the task exists by calling the API
                HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTask/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["errorMessage"] = $"Task not found with ID: {id}";
                    return Redirect(Request.Headers["Referer"].ToString());
                }

                // Deserialize the task data
                string taskData = await response.Content.ReadAsStringAsync();
                TasksViewModel task = JsonConvert.DeserializeObject<TasksViewModel>(taskData);

                if (task == null)
                {
                    TempData["errorMessage"] = $"Task not found with ID: {id}";
                    return Redirect(Request.Headers["Referer"].ToString());
                }

                // Check if the current status is "Të Vonuara"
                if (task.Status == "Të Vonuara")
                {
                    TempData["errorMessage"] = "Task status cannot be changed as it is marked as 'Të Vonuara'.";
                    return Redirect(Request.Headers["Referer"].ToString());
                }

                // Send the POST request to change the task status
                HttpResponseMessage updateResponse = await _client.PostAsync($"{_client.BaseAddress}/Tasks/ChangeTaskStatus/changestatus/{id}", null);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    string errorMsg = await updateResponse.Content.ReadAsStringAsync();
                    TempData["errorMessage"] = $"Failed to update task status: {errorMsg}";
                }
                else
                {
                    TempData["successMessage"] = "Task status updated successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"An error occurred: {ex.Message}";
            }

            return Redirect(Request.Headers["Referer"].ToString());
        }

        private async Task<List<TasksViewModel>> GetTasks()
        {
            List<TasksViewModel> tasksList = new List<TasksViewModel>();
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            // Fetch tasks from the backend
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetAllTasks");

            // Fetch users for mapping purposes
            HttpResponseMessage usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");

            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = await usersResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            if (tasksResponse.IsSuccessStatusCode)
            {
                string data = await tasksResponse.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(data);

                foreach (var task in tasksList)
                {
                    // Map AssignedTo ID to FullName for display
                    var assignedUser = usersList.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    var CreatedTaskBy = usersList.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";


                    // Check for overdue tasks and update status if necessary
                    if (task.Status != "Të Përfunduara")
                    {
                        if (task.DueDate < DateTime.Now)
                        {
                            if (task.Status != "Të Vonuara")
                            {
                                task.Status = "Të Vonuara";
                            }

                        }
                        
                    }

                    // Serialize and send updated task to the backend
                    string updatedTaskData = JsonConvert.SerializeObject(task);
                    StringContent content = new StringContent(updatedTaskData, Encoding.UTF8, "application/json");

                    // Send the PUT request to update the task status in the backend
                    await _client.PutAsync($"{_client.BaseAddress}/Tasks/UpdateTask/{task.Id}", content);
                }
            }

            return tasksList;
        }

        private async Task<List<TasksViewModel>> GetTasksByTheUserId()
        {
            // Retrieve the current user's username from the session
            string username = HttpContext.Session.GetString("Username");

            // If username is not found, redirect to login
            if (string.IsNullOrEmpty(username))
            {
                return new List<TasksViewModel>(); // Return empty if no user is logged in
            }

            List<TasksViewModel> tasksList = new List<TasksViewModel>();
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            // Fetch users for mapping purposes
            HttpResponseMessage usersResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = await usersResponse.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            // Find the current user's ID
            var currentUser = usersList.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return new List<TasksViewModel>(); // Return empty if the user is not found
            }

            // Fetch tasks assigned to the current user
            HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasks/user/{currentUser.Id}");
            if (tasksResponse.IsSuccessStatusCode)
            {
                string data = await tasksResponse.Content.ReadAsStringAsync();
                tasksList = JsonConvert.DeserializeObject<List<TasksViewModel>>(data);

                foreach (var task in tasksList)
                {
                    // Map AssignedTo ID to FullName for display
                    var assignedUser = usersList.FirstOrDefault(u => u.Id == task.AssignedTo);
                    task.AssignedToName = assignedUser != null ? assignedUser.FullName : "Unassigned";

                    var CreatedTaskBy = usersList.FirstOrDefault(u => u.Id == task.CreatedBy);
                    task.CreatedByName = CreatedTaskBy != null ? CreatedTaskBy.FullName : "Unassigned";

                    // Check for overdue tasks and update status if necessary
                    if (task.Status != "Të Përfunduara")
                    {
                        if (task.DueDate < DateTime.Now)
                        {
                            if (task.Status != "Të Vonuara")
                            {
                                task.Status = "Të Vonuara";
                            }

                        }

                    }

                    // Serialize and send updated task to the backend
                    string updatedTaskData = JsonConvert.SerializeObject(task);
                    StringContent content = new StringContent(updatedTaskData, Encoding.UTF8, "application/json");

                    // Send the PUT request to update the task status in the backend
                    await _client.PutAsync($"{_client.BaseAddress}/Tasks/UpdateTask/{task.Id}", content);
                }
            }

            return tasksList;
        }
    }
}
