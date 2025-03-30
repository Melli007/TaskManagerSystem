using ETechTaskManager.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Newtonsoft.Json;
using EtechTaskManagerBackend.DTO;
using Microsoft.AspNetCore.Identity;
using System.Text;


namespace ETechTaskManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger; 

        //Konstruktori per inicializimin e loggerit
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        //Na Shfaq Faqen
        public IActionResult Login()
        {
            return View();
        }

        // Kontrolleri i loginit
 [HttpPost]
public async Task<IActionResult> Login(string username, string password)
{
    HttpResponseMessage response = await new HttpClient().GetAsync("https://localhost:7013/api/Users/GetUsers");

            if (response.IsSuccessStatusCode)
            {

                string data = await response.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<UsersDTO>>(data);
                var user = users.FirstOrDefault(u => u.Username == username || u.Email == username); // Gjejm perdorusin pi usernamit ose emailit


                if (user != null)
                {
                    //Verifikimi i passwordit tu e perdor PasswordHasher
                    var passwordHasher = new PasswordHasher<UsersDTO>();
                    var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, password);

                    if (passwordVerificationResult == PasswordVerificationResult.Success)
                    {
                        // Set the user's IsOnline status to true
                        using (var client = new HttpClient())
                        {
                            var onlineStatusResponse = await client.PutAsync(
                                $"https://localhost:7013/api/Users/UpdateOnlineStatus/{user.Id}/online-status",
                                new StringContent(JsonConvert.SerializeObject(true), Encoding.UTF8, "application/json")
                            );

                        }

                        //Krijimi i cookies per ruajtjen e informacionit te perdorusit
                        Response.Cookies.Append("UserId", user.Id.ToString(), new CookieOptions
                        {
                            Expires = DateTimeOffset.Now.AddDays(7), // Koha e skadimit te cookit
                            HttpOnly = true,                         //  Cookie mundet mu aksesu vetem nga serveri
                            Secure = true,                   //Lejon perdorimin vetem ne HTTPS
                            SameSite = SameSiteMode.Strict  // Aktivizion rregullat e "SameSite"
                        });

                        //Ruajtja e te dhenave ne sesione per perdorusin
                        HttpContext.Session.SetString("Username", user.Username);
                        HttpContext.Session.SetString("UserRole", user.Role);
                        HttpContext.Session.SetString("UserId", user.Id.ToString());
                        HttpContext.Session.SetString("IsBanned", user.Banned.ToString());
                        HttpContext.Session.SetString("UserProfession", user.Profession ?? "");

                        ViewData["UserId"] = user.Id.ToString(); //Kalon the dhenat pi kontrollerit ne mvc

                        // Kthen ne faqe te indexit ku percaktohet a do shfaq pjesen e admit apo te userit ne varesi te rolit qe kemi ber login
                        return user.Role == "Admin"
                            ? RedirectToAction("Index") 
                            : RedirectToAction("Index"); 
                    }
                }
            }

    ModelState.AddModelError("", "Kredencialet e pavlefshme të hyrjes.");
    return View();
}

        public async Task<IActionResult> Index()
        {

            string userRole = HttpContext.Session.GetString("UserRole") ?? "user";
            string userId = HttpContext.Session.GetString("UserId"); // Retrieve UserId
            ViewData["UserRole"] = userRole;

            // Fetch number of users
            using (var client = new HttpClient())
            {
                var userResponse = await client.GetAsync("https://localhost:7013/api/Users/GetUsers");
                if (userResponse.IsSuccessStatusCode)
                {
                    string userData = await userResponse.Content.ReadAsStringAsync();
                    var users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);

                    // Exclude users with the "Admin" role
                    var nonAdminUsers = users.Where(u =>
                        string.IsNullOrEmpty(u.Role) || !u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)).ToList();

                    ViewData["num_users"] = nonAdminUsers.Count; // Number of users

                    // Find the logged-in user from the list
                    var currentUser = users.FirstOrDefault(u => u.Id.ToString() == userId);
                    if (currentUser != null)
                    {
                        // Update the IsBanned session variable dynamically
                        HttpContext.Session.SetString("IsBanned", currentUser.Banned.ToString());
                    }
                }
                else
                {
                    ViewData["num_users"] = 0; // Fallback in case of error
                }

                // Fetch all tasks
                var taskResponse = await client.GetAsync("https://localhost:7013/api/Tasks/GetAllTasks"); // Adjust this endpoint as necessary
                if (taskResponse.IsSuccessStatusCode)
                {
                    string taskData = await taskResponse.Content.ReadAsStringAsync();
                    var tasks = JsonConvert.DeserializeObject<List<TasksDTO>>(taskData); // Adjust this DTO as necessary

                    // Process task statuses
                    foreach (var task in tasks)
                    {
                        if (task.Status != "Të Përfunduara")
                        {
                            if (task.DueDate < DateTime.Now && task.Status != "Të Vonuara")
                            {
                                task.Status = "Të Vonuara";
                            }
                        }
                    }

                    // Count tasks by their statuses
                    ViewData["num_task"] = tasks.Count; // Total tasks
                    ViewData["overdue_task"] = tasks.Count(t => t.Status == "Të Vonuara");
                    ViewData["nodeadline_task"] = tasks.Count(t => t.DueDate == null); // No deadline tasks
                    ViewData["todaydue_task"] = tasks.Count(t => t.DueDate?.Date == DateTime.Today); // Tasks due today
                    ViewData["pending"] = tasks.Count(t => t.Status == "Në Pritje"); // Pending tasks
                    ViewData["in_progress"] = tasks.Count(t => t.Status == "Në Progres"); // In progress tasks
                    ViewData["completed"] = tasks.Count(t => t.Status == "Të Përfunduara"); // Completed tasks

                    // Additional Admin metrics
                    ViewData["weekly_tasks"] = tasks.Count(t => t.CreatedAt >= DateTime.Now.AddDays(-7));     
                    ViewData["old_pending"] = tasks.Count(t => t.Status == "Në Pritje" && t.CreatedAt < DateTime.Now.AddDays(-3));
                    ViewData["weekly_completed"] = tasks.Count(t => t.Status == "Të Përfunduara" && t.CreatedAt >= DateTime.Now.AddDays(-7));
                }
                else
                {
                    ViewData["num_task"] = 0; // Fallback in case of error
                    ViewData["overdue_task"] = 0;
                    ViewData["nodeadline_task"] = 0;
                    ViewData["todaydue_task"] = 0;
                    ViewData["pending"] = 0;
                    ViewData["in_progress"] = 0;
                    ViewData["completed"] = 0;

                    ViewData["weekly_tasks"] = 0;
                    ViewData["old_pending"] = 0;
                    ViewData["active_in_progress"] = 0;
                    ViewData["weekly_completed"] = 0;
                }

                // Fetch user-specific tasks
                var userTasksResponse = await client.GetAsync($"https://localhost:7013/api/Tasks/GetTasks/user/{userId}"); // Ensure this endpoint is correct
                if (userTasksResponse.IsSuccessStatusCode)
                {
                    string userTaskData = await userTasksResponse.Content.ReadAsStringAsync();
                    var userTasks = JsonConvert.DeserializeObject<List<TasksDTO>>(userTaskData); // Adjust this DTO as necessary

                    foreach (var task in userTasks)
                    {

                        if (task.Status != "Të Përfunduara")
                        {
                            if (task.DueDate < DateTime.Now && task.Status != "Të Vonuara")
                            {
                                task.Status = "Të Vonuara";
                            }
                        }
                    }

                    ViewData["num_taskk"] = userTasks.Count; // Total tasks for the user
                    ViewData["overdue_taskk"] = userTasks.Count(t => t.Status == "Të Vonuara");
                    ViewData["nodeadline_taskk"] = userTasks.Count(t => t.DueDate == null); // No deadline tasks for the user
                    ViewData["todaydue_taskk"] = userTasks.Count(t => t.DueDate?.Date == DateTime.Today); // Tasks due today for the user
                    ViewData["pendingg"] = userTasks.Count(t => t.Status == "Në Pritje"); // Pending tasks for the user
                    ViewData["in_progresss"] = userTasks.Count(t => t.Status == "Në Progres"); // In progress tasks for the user
                    ViewData["completedd"] = userTasks.Count(t => t.Status == "Të Përfunduara"); // Completed tasks for the user

                    ViewData["high_priority"] = userTasks.Count(t =>
                    {
                        // Skip tasks with status "Të Përfunduara" or "Të Vonuara"
                        if (t.Status == "Të Përfunduara" || t.Status == "Të Vonuara")
                        {
                            return false;
                        }

                        // Skip tasks without a deadline or creation date
                        if (t.DueDate == null)
                        {
                            return false;
                        }

                        // Calculate total time and elapsed time
                        TimeSpan totalTime = t.DueDate.Value - t.CreatedAt; // Total time between creation and deadline
                        TimeSpan elapsedTime = DateTime.Now - t.CreatedAt; // Time elapsed since creation

                        // Check if 50% of the time has passed
                        return elapsedTime >= totalTime / 2;
                    });
                    ViewData["my_weekly_completed"] = userTasks.Count(t => t.Status == "Të Përfunduara" && t.CreatedAt >= DateTime.Now.AddDays(-7));
                }
                else
                {
                    ViewData["num_taskk"] = 0; // Fallback in case of error
                    ViewData["overdue_taskk"] = 0;
                    ViewData["nodeadline_taskk"] = 0;
                    ViewData["todaydue_taskk"] = 0;
                    ViewData["pendingg"] = 0;
                    ViewData["in_progresss"] = 0;
                    ViewData["completedd"] = 0;

                    ViewData["high_priority"] = 0;
                    ViewData["my_weekly_completed"] = 0;
                }
                 
                // Fetch notifications
                var notificationResponse = await client.GetAsync($"https://localhost:7013/api/Notifications/GetAllNotificationsForAdmin?createdBy={userId}");
                if (notificationResponse.IsSuccessStatusCode)
                {
                    string notificationData = await notificationResponse.Content.ReadAsStringAsync();
                    var notifications = JsonConvert.DeserializeObject<List<NotificationsDTO>>(notificationData);
                    ViewData["num_notifications"] = notifications.Count; // Number of notifications

                    // Count unread
                    var unreadCount = notifications.Count(n => !n.IsRead);
                    ViewData["unread_notifications"] = unreadCount;
                }
                else
                {
                    ViewData["num_notifications"] = 0; // Fallback in case of error
                    ViewData["unread_notifications"] = 0; // fallback
                }

                // Fetch online users
                var onlineUsersResponse = await client.GetAsync("https://localhost:7013/api/Users/GetOnlineUsers/online-users");
                if (onlineUsersResponse.IsSuccessStatusCode)
                {
                    string onlineUsersData = await onlineUsersResponse.Content.ReadAsStringAsync();
                    Console.WriteLine("Online Users Data: " + onlineUsersData); // Debug log

                    var onlineUsers = JsonConvert.DeserializeObject<List<UsersDTO>>(onlineUsersData);
                    Console.WriteLine("Deserialized Online Users Count: " + onlineUsers.Count); // Debug log

                    // Filter out admins
                    var nonAdminOnlineUsers = onlineUsers
                        .Where(u => string.IsNullOrEmpty(u.Role) || !u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    ViewData["online_users"] = nonAdminOnlineUsers; // Pass filtered online users to the view
                }
                else
                {
                    Console.WriteLine("Failed to fetch online users. Status Code: " + onlineUsersResponse.StatusCode); // Debug log
                    ViewData["online_users"] = 0; // Fallback in case of error
                }
            }

            return View();
        }

        public IActionResult Logout()
        {
            string userId = HttpContext.Session.GetString("UserId");

            if (!string.IsNullOrEmpty(userId))
            {
                using (var client = new HttpClient())
                {
                    var onlineStatusResponse = client.PutAsync(
                        $"https://localhost:7013/api/Users/UpdateOnlineStatus/{userId}/online-status",
                        new StringContent(JsonConvert.SerializeObject(false), Encoding.UTF8, "application/json")
                    ).Result;

                    if (!onlineStatusResponse.IsSuccessStatusCode)
                    {
                        TempData["Error"] = "Failed to update user's online status.";
                    }
                }
            }

            HttpContext.Session.Clear();  // Clears the session for the tab
            return RedirectToAction("Login");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Profile()
        {
            string userId = HttpContext.Session.GetString("UserId"); // Retrieve UserId
            ViewData["UserId"] = userId;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            using (var client = new HttpClient())
            {
                var userByIdResponse = await client.GetAsync($"https://localhost:7013/api/Users/GetUserById/{userId}");
                var tasksByUserID = await client.GetAsync($"https://localhost:7013/api/Tasks/GetTasksAssignedToUser/assignedtouser/{userId}");

                if (userByIdResponse.IsSuccessStatusCode)
                {
                    string userData = await userByIdResponse.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UsersViewModel>(userData);

                    string tasksData = await tasksByUserID.Content.ReadAsStringAsync();
                    var tasks = JsonConvert.DeserializeObject<List<TasksDTO>>(tasksData);
                    user.Tasks = tasks;
                    // Directly pass the user details to the view
                    return View(user);
                }
                else
                {
                    // Handle error or display a message if the user data can't be fetched
                    ViewData["Error"] = "Could not fetch user details.";
                }

            }

            return View();
        }

        [HttpGet]
        public IActionResult EnterOldPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EnterOldPassword(string oldPassword)
        {
            string userId = HttpContext.Session.GetString("UserId"); // Retrieve UserId

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            // Get the user details
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"https://localhost:7013/api/Users/GetUserById/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    string userData = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UsersDTO>(userData);

                    // Verify the old password using PasswordHasher
                    var passwordHasher = new PasswordHasher<UsersDTO>();
                    var verificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, oldPassword);

                    if (verificationResult == PasswordVerificationResult.Success)
                    {
                        // Password is correct, redirect to EditProfile
                        TempData["OldPasswordValidated"] = true;
                        return RedirectToAction("EditProfile");
                    }
                    else
                    {
                        // Password is incorrect, show an error message
                        ViewData["Error"] = "Incorrect old password. Please try again.";
                        return View();
                    }
                }
                else
                {
                    // Handle error if user data cannot be fetched
                    ViewData["Error"] = "Could not fetch user details.";
                    return View();
                }
            }
        }


        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            string userId = HttpContext.Session.GetString("UserId"); // Retrieve UserId

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            // Retrieve TempData to check if the old password was validated successfully
            if (TempData["OldPasswordValidated"] != null)
            {
                ViewData["SuccessMessage"] = "Old password validated successfully!";
            }

            using (var client = new HttpClient())
            {
                var userByIdResponse = await client.GetAsync($"https://localhost:7013/api/Users/GetUserById/{userId}");

                if (userByIdResponse.IsSuccessStatusCode)
                {
                    string userData = await userByIdResponse.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserUpdateModel>(userData);
                    return View(user); // Pass user details to the view
                }
                else
                {
                    ViewData["Error"] = "Could not fetch user details.";
                    return RedirectToAction("Profile");
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(UserUpdateModel updatedUser)
        {
            string userId = HttpContext.Session.GetString("UserId"); // Retrieve UserId

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(updatedUser); // Re-display form with validation errors
            }

            using (var client = new HttpClient())
            {
                var jsonUserData = JsonConvert.SerializeObject(updatedUser);
                var content = new StringContent(jsonUserData, Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"https://localhost:7013/api/Users/UpdateUsersInfo/{userId}", content);

                if (response.IsSuccessStatusCode)
                {
                    // If a new password is provided, update the password
                    if (!string.IsNullOrEmpty(updatedUser.Password))
                    {
                        var passwordUpdateResponse = await client.PostAsync(
                                           $"https://localhost:7013/api/Users/UpdatePassword/{userId}",
                                           new StringContent(JsonConvert.SerializeObject(new { NewPassword = updatedUser.Password }), Encoding.UTF8, "application/json")
                                       );

                        if (!passwordUpdateResponse.IsSuccessStatusCode)
                        {
                            ViewData["Error"] = "Failed to update password.";
                            return View(updatedUser);
                        }
                    }
                    return RedirectToAction("Profile");
                }
                else
                {
                    ViewData["Error"] = "Failed to update user details.";
                    return View(updatedUser);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> SendResetLink()
        {
            // 1) Get the user’s ID from Session
            string userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                // If somehow the session is empty, redirect to login
                return RedirectToAction("Login");
            }

            // 2) Fetch the user from the API to get the user's email
            UsersDTO user;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"https://localhost:7013/api/Users/GetUserById/{userId}");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Could not fetch user details.";
                    return RedirectToAction("EnterOldPassword");
                }

                var json = await response.Content.ReadAsStringAsync();
                user = JsonConvert.DeserializeObject<UsersDTO>(json);
            }

            // 3) Generate a unique reset key (GUID)
            string resetKey = Guid.NewGuid().ToString();

            // 4) Save the resetKey in the database for this user
            using (var client = new HttpClient())
            {
                var saveResetKeyContent = new StringContent(
                    JsonConvert.SerializeObject(new { Email = user.Email, ResetKey = resetKey }),
                    Encoding.UTF8,
                    "application/json"
                );

                var saveResetKeyResponse = await client.PostAsync("https://localhost:7013/api/Users/SaveResetKey", saveResetKeyContent);
                if (!saveResetKeyResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Failed to generate reset key. Please try again.";
                    return RedirectToAction("EnterOldPassword");
                }
            }

            // 5) Build the reset link that points to a ResetPassword action (below)
            string resetLink = Url.Action("ResetPassword", "Home", new { resetKey }, Request.Scheme);

            // 6) Send the email
            using (var client = new HttpClient())
            {
                var emailContent = new
                {
                    To = user.Email,
                    Subject = "Password Reset",
                    Body = $"Click <a href='{resetLink}'>here</a> to reset your password."
                };

                var emailResponse = await client.PostAsync(
                    "https://localhost:7013/api/Users/SendEmail",
                    new StringContent(JsonConvert.SerializeObject(emailContent), Encoding.UTF8, "application/json")
                );

                if (!emailResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Failed to send reset email. Please try again.";
                    return RedirectToAction("EnterOldPassword");
                }
            }

            // Give user some feedback, e.g., redirect back or show success
            TempData["Success"] = "We’ve sent a password reset link to your email.";
            return RedirectToAction("EditProfile");
        }

        [HttpGet]
        public IActionResult ResetPassword(string resetKey)
        {
            if (string.IsNullOrEmpty(resetKey))
            {
                ViewData["Error"] = "Invalid reset key.";
                return View();
            }

            // Provide the key to the view via ViewData or ViewBag
            ViewData["ResetKey"] = resetKey;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string resetKey, string newPassword)
        {
            if (string.IsNullOrEmpty(resetKey) || string.IsNullOrEmpty(newPassword))
            {
                ViewData["Error"] = "Both reset key and new password are required.";
                return View();
            }

            using (var client = new HttpClient())
            {
                var resetRequestBody = new { ResetKey = resetKey, NewPassword = newPassword };

                var response = await client.PostAsync(
                    "https://localhost:7013/api/Users/ResetPassword",
                    new StringContent(JsonConvert.SerializeObject(resetRequestBody), Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    ViewData["Error"] = "Failed to reset password. The link may be invalid or expired.";
                    return View();
                }
            }

            ViewData["Success"] = "Password has been reset successfully!";
            return RedirectToAction("EditProfile");
        }

    }
}
