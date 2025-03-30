using ETechTaskManager.Models;
using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace ETechTaskManager.Controllers
{
    public class UsersController : Controller
    {
        Uri baseAddress = new Uri("https://localhost:7013/api");

        private readonly HttpClient _client;

        public UsersController()
        {
            _client = new HttpClient();
            _client.BaseAddress = baseAddress;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
              string sortOrder = null,  // "asc" or "desc" for CreatedAt
    DateTime? startDate = null,
    DateTime? endDate = null,
    string onlineStatus = null // can be "online", "offline", or null for all
            )
        {   

            List<UsersViewModel> usersList = new List<UsersViewModel>();
            HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUsers");


            if (response.IsSuccessStatusCode)
            {
                string data = await response.Content.ReadAsStringAsync();
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(data);

                // Filter out users with the role "ADMIN"
                usersList = usersList.Where(user =>
                    user.Role == null || !user.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // 1) Filter by date range (CreatedAt)
                if (startDate.HasValue)
                {
                    usersList = usersList
                        .Where(u => u.CreatedAt >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    usersList = usersList
                        .Where(u => u.CreatedAt <= endDate.Value)
                        .ToList();
                }

                // 2) Filter by online/offline
                //    Assume your UsersViewModel has a property "IsOnline" (bool)
                //    If you store it differently, adjust as needed
                if (!string.IsNullOrEmpty(onlineStatus))
                {
                    if (onlineStatus == "online")
                    {
                        // Show only online users
                        usersList = usersList
                            .Where(u => u.IsOnline)
                            .ToList();
                    }
                    else if (onlineStatus == "offline")
                    {
                        // Show only offline users
                        usersList = usersList
                            .Where(u => !u.IsOnline)
                            .ToList();
                    }
                }

                // 3) Sort by CreatedAt asc/desc
                if (sortOrder == "asc")
                {
                    usersList = usersList
                        .OrderBy(u => u.CreatedAt)
                        .ToList();
                }
                else if (sortOrder == "desc")
                {
                    usersList = usersList
                        .OrderByDescending(u => u.CreatedAt)
                        .ToList();
                }
            }
            return View(usersList);
        }

        [HttpGet]
        public async Task<IActionResult> Leaderboard(string period = "all", string profession = null)
        {
            // We assume you've set this in your login or a base controller.
            var userRole = ViewData["UserRole"]?.ToString() ?? "User";

            // 2. Fetch all users from your API
            HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUsers");
            if (!response.IsSuccessStatusCode)
            {
                TempData["errorMessage"] = "Failed to load users.";
                return RedirectToAction("Index", "Home");
            }

            // 3. Parse them into a local list
            string data = await response.Content.ReadAsStringAsync();
            var users = JsonConvert.DeserializeObject<List<UsersViewModel>>(data);

            // 4. If the user is NOT Admin, find current user in "users" and force the profession
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Just read from Session:
                var userProfession = HttpContext.Session.GetString("UserProfession");
                if (!string.IsNullOrWhiteSpace(userProfession))
                {
                    profession = userProfession;
                }
            }

            // 5. Pass final period/profession to the view
            ViewBag.Period = period;
            ViewBag.Profession = profession;

            // 6. Identify admin user IDs
            var adminUserIds = users
                .Where(u => string.Equals(u.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Id)
                .ToHashSet();

            // 7. Exclude admin users from the leaderboard
            users = users
                .Where(u => !string.Equals(u.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 8. If there's a profession filter, apply it
            if (!string.IsNullOrEmpty(profession))
            {
                users = users
                  .Where(u => !string.IsNullOrEmpty(u.Profession)
                           && u.Profession.Trim().Equals(profession.Trim(), StringComparison.OrdinalIgnoreCase))
                  .ToList();
            }

            // 9. For each user, fetch tasks, etc.
            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    user.ProfilePicturePath = "/Images/user.png";
                }

                HttpResponseMessage tasksResponse = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasks/user/{user.Id}");
                if (!tasksResponse.IsSuccessStatusCode)
                {
                    user.Tasks = new List<TasksDTO>();
                    user.CompletedTasks = 0;
                    user.InProgressTasks = 0;
                    user.PendingTasks = 0;
                    user.OverdueTasks = 0;
                    user.TotalTasks = 0;
                    user.PerformancePercentage = 0;
                    continue;
                }

                string tasksData = await tasksResponse.Content.ReadAsStringAsync();
                var allTasks = JsonConvert.DeserializeObject<List<TasksDTO>>(tasksData);

                // Only tasks created by admin
                var adminTasks = allTasks.Where(t => adminUserIds.Contains(t.CreatedBy)).ToList();

                // Filter tasks by period
                var filteredTasks = FilterTasksByPeriod(adminTasks, period);
                user.Tasks = filteredTasks;

                user.CompletedTasks = user.Tasks.Count(t => t.Status == "Të Përfunduara");
                user.InProgressTasks = user.Tasks.Count(t => t.Status == "Në Progres");
                user.PendingTasks = user.Tasks.Count(t => t.Status == "Në Pritje");
                user.OverdueTasks = user.Tasks.Count(t => t.Status == "Të Vonuara");
                user.TotalTasks = user.Tasks.Count();

                double performanceScore = (user.TotalTasks == 0)
                    ? 0
                    : ((user.CompletedTasks * 1.0)
                       + (user.InProgressTasks * 0.5)
                       - (user.OverdueTasks * 0.75)) / user.TotalTasks * 100;

                user.PerformancePercentage = Math.Max(0, Math.Round(performanceScore, 2));
            }

            // 10. Sort and take top 10
            var topEmployees = users
                .Where(u => u.TotalTasks > 0)
                .OrderByDescending(u => (u.PerformancePercentage * 0.6) + (Math.Sqrt(u.TotalTasks) * 0.3) - (u.OverdueTasks * 0.1))
                .Take(10)
                .ToList();

            return View(topEmployees);
        }

        private List<TasksDTO> FilterTasksByPeriod(List<TasksDTO> tasks, string period)
        {
            var now = DateTime.Now;

            return period switch
            {
                "week" => tasks.Where(t => t.CreatedAt >= now.AddDays(-7)).ToList(),
                "month" => tasks.Where(t => t.CreatedAt >= now.AddMonths(-1)).ToList(),
                "year" => tasks.Where(t => t.CreatedAt >= now.AddYears(-1)).ToList(),
                _ => tasks
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetUserTasks(int userId, string period = "all")
        {
            HttpResponseMessage response = await _client.GetAsync($"{_client.BaseAddress}/Tasks/GetTasks/user/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                return Json(new List<TasksDTO>()); // Return empty list if API fails
            }

            string tasksData = await response.Content.ReadAsStringAsync();
            var allTasks = JsonConvert.DeserializeObject<List<TasksDTO>>(tasksData);

            // Apply filtering for sidebar
            var filteredTasks = FilterTasksByPeriod(allTasks, period);

            // Fetch all users to get their roles
            HttpResponseMessage usersResponse = await _client.GetAsync(_client.BaseAddress + "/Users/GetUsers");
            if (!usersResponse.IsSuccessStatusCode)
            {
                return Json(new List<TasksDTO>()); // Return empty if user data fails to load
            }

            string usersData = await usersResponse.Content.ReadAsStringAsync();
            var allUsers = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);

            // Get all ADMIN user IDs
            var adminUserIds = allUsers
                .Where(u => string.Equals(u.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Id)
                .ToHashSet(); // Use HashSet for faster lookup

            // Filter tasks where CreatedBy is an ADMIN
            var adminTasks = filteredTasks.Where(t => adminUserIds.Contains(t.CreatedBy)).ToList();

            return Json(adminTasks);
        }


        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUsers");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["errorMessage"] = "Failed to fetch users.";
                    return RedirectToAction("Index");
                }

                string data = await response.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<UsersViewModel>>(data);

                // Filter out users with the role "ADMIN"
                users = users.Where(user => user.Role == null || !user.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)).ToList();

                // Ensure EPPlus license is set for non-commercial use
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Users");

                    // Styling for the header row
                    worksheet.Cells[1, 1].Value = "Id";
                    worksheet.Cells[1, 2].Value = "Username";
                    worksheet.Cells[1, 3].Value = "Email";
                    worksheet.Cells[1, 4].Value = "Phone";
                    worksheet.Cells[1, 5].Value = "Role";
                    worksheet.Cells[1, 6].Value = "Profession";

                    using (var headerRange = worksheet.Cells[1, 1, 1, 6])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Font.Size = 14;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        headerRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        headerRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        headerRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        headerRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }

                    // Populate rows with user data
                    int row = 2;
                    foreach (var user in users)
                    {
                        worksheet.Cells[row, 1].Value = user.Id;
                        worksheet.Cells[row, 2].Value = user.Username;
                        worksheet.Cells[row, 3].Value = user.Email;
                        worksheet.Cells[row, 4].Value = user.Phone;
                        worksheet.Cells[row, 5].Value = user.Role;
                        worksheet.Cells[row, 6].Value = user.Profession;

                        // Style rows for readability
                        using (var rowRange = worksheet.Cells[row, 1, row, 6])
                        {
                            rowRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        }
                        row++;
                    }

                    // Auto-fit columns and add some visual polish
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    worksheet.Cells.Style.Font.Name = "Calibri";

                    // Save the Excel package to a memory stream
                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    string fileName = "Users.xlsx";

                    return File(stream, contentType, fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error generating Excel file: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult Create()
        { 
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(UsersViewModel model)
        {
            try
            {
                string data = JsonConvert.SerializeObject(model);
                StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _client.PostAsync(_client.BaseAddress + "/Users/CreateUsers", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "User Created.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message; 
                return View();
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                UsersViewModel user = new UsersViewModel();
                HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUserById/" + id);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    user = JsonConvert.DeserializeObject<UsersViewModel>(data);
                }
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UsersViewModel model)
        {
            try
            {
                string data = JsonConvert.SerializeObject(model);
                StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _client.PutAsync(_client.BaseAddress + "/Users/UpdateUsers/" + model.Id, content);


                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "users details updated. ";
                    return RedirectToAction("Index");
                }
                
            }
            catch (Exception ex)
            {

                TempData["errorMessage"] = ex.Message;
                return View();
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                UsersViewModel user = new UsersViewModel();
                HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUserById/" + id);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    user = JsonConvert.DeserializeObject<UsersViewModel>(data);
                }
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return View();
            }
        }

        [HttpPost,ActionName("Delete")]
        public async Task <IActionResult> DeleteConfirmed(int id)
        {
            try
            {
    
                HttpResponseMessage response = await _client.DeleteAsync(_client.BaseAddress + "/Users/DeleteUser/" + id);


                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "users details deleted. ";
                    return RedirectToAction("Index");
                }

            }
            catch (Exception ex)
            {

                TempData["errorMessage"] = ex.Message;
                return View();
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Ban(int id)
        {
            try
            {
                // Fetch user details for confirmation
                UsersViewModel user = new UsersViewModel();
                HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUserById/" + id);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    user = JsonConvert.DeserializeObject<UsersViewModel>(data);
                }
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ActionName("Ban")]
        public async Task<IActionResult> BanConfirmed(int id)
        {
            try
            {
                HttpResponseMessage response = await _client.PutAsync(_client.BaseAddress + "/Users/BanUser/" + id, null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "User banned successfully.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Unban(int id)
        {
            try
            {
                // Fetch user details for confirmation
                UsersViewModel user = new UsersViewModel();
                HttpResponseMessage response = await _client.GetAsync(_client.BaseAddress + "/Users/GetUserById/" + id);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    user = JsonConvert.DeserializeObject<UsersViewModel>(data);
                }
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ActionName("Unban")]
        public async Task<IActionResult> UnbanConfirmed(int id)
        {
            try
            {
                HttpResponseMessage response = await _client.PutAsync(_client.BaseAddress + "/Users/UnbanUser/" + id, null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "User unbanned successfully.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            return RedirectToAction("Index");
        }


    }
}
