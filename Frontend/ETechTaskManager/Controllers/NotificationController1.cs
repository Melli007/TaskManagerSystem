using Azure;
using ETechTaskManager.Models;
using EtechTaskManagerBackend.DTO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace ETechTaskManager.Controllers
{
    public class NotificationController1 : Controller
    {
        Uri baseAddress = new Uri("https://localhost:7013/api");

        private readonly HttpClient _client;

        public NotificationController1()
        {
            _client = new HttpClient();
            _client.BaseAddress = baseAddress;
        }

        [HttpGet]
        public IActionResult Index(
    string sortOrder = null,        // "asc" or "desc"
    DateTime? startDate = null,     // optional start date
    DateTime? endDate = null,       // optional end date
    bool? isReadOnly = null         // for filtering read/unread 
)
        {
            // Retrieve the admin's ID from the session
            var currentUserId = HttpContext.Session.GetString("UserId");

            List<NotificationsViewModel> notificationsList = new List<NotificationsViewModel>();
            List<UsersViewModel> usersList = new List<UsersViewModel>();

            HttpResponseMessage notificationsResponse = _client.GetAsync($"{_client.BaseAddress}/Notifications/GetAllNotificationsForAdmin?createdBy={currentUserId}").Result;

            // Fetch users for mapping purposes
            HttpResponseMessage usersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;

            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = usersResponse.Content.ReadAsStringAsync().Result;
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            if (notificationsResponse.IsSuccessStatusCode)
            {
                string data = notificationsResponse.Content.ReadAsStringAsync().Result;
                notificationsList = JsonConvert.DeserializeObject<List<NotificationsViewModel>>(data);

                foreach (var notification in notificationsList)
                {
                    var recipientUser = usersList.FirstOrDefault(u => u.Id == notification.Recipient);
                    notification.RecipientName = recipientUser != null ? recipientUser.FullName : "Unknown";
                }

                // (a) Date Range (by notification.Date)
                if (startDate.HasValue)
                {
                    notificationsList = notificationsList
                        .Where(n => n.Date >= startDate.Value)
                        .ToList();
                }
                if (endDate.HasValue)
                {
                    notificationsList = notificationsList
                        .Where(n => n.Date <= endDate.Value)
                        .ToList();
                }

                // (b) Filter read/unread if desired
                //    e.g. isReadOnly = true means show only "IsRead == true"
                if (isReadOnly.HasValue)
                {
                    if (isReadOnly.Value)
                    {
                        // Show only read
                        notificationsList = notificationsList
                            .Where(n => n.IsRead == true)
                            .ToList();
                    }
                    else
                    {
                        // Show only unread
                        notificationsList = notificationsList
                            .Where(n => n.IsRead == false)
                            .ToList();
                    }
                }

                // (c) Sort by Date ascending or descending
                if (sortOrder == "asc")
                {
                    notificationsList = notificationsList.OrderBy(n => n.Date).ToList();
                }
                else if (sortOrder == "desc")
                {
                    notificationsList = notificationsList.OrderByDescending(n => n.Date).ToList();
                }
            }


            return View(notificationsList);
        }


        [HttpGet]
        public IActionResult GetNotifications()
        {
            // Retrieve the current user's ID from the session
            string username = HttpContext.Session.GetString("Username");

            if (username == null)
            {
                return RedirectToAction("Login", "Home"); // Redirect to login if user ID is not found
            }

            // Fetch the user to get the ID based on the username
            HttpResponseMessage userResponse = _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers").Result;
            List<UsersDTO> users = new List<UsersDTO>();

            if (userResponse.IsSuccessStatusCode)
            {
                string userData = userResponse.Content.ReadAsStringAsync().Result;
                users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);
            }

            // Find the current user's ID
            var currentUser = users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Home");
            }

            List<NotificationsViewModel> userNotifications = new List<NotificationsViewModel>();

            // Make a GET request to fetch notifications for the specified user ID
            HttpResponseMessage response = _client.GetAsync($"{_client.BaseAddress}/Notifications/GetNotifications/user/{currentUser.Id}").Result;

            if (response.IsSuccessStatusCode)
            {
                string data = response.Content.ReadAsStringAsync().Result;
                userNotifications = JsonConvert.DeserializeObject<List<NotificationsViewModel>>(data);
            }
            else
            {
                // Handle the case where the request failed
                ModelState.AddModelError("", "Could not retrieve notifications for the specified user.");
            }


            ViewData["UserId"] = currentUser.Id;

            return PartialView("GetNotifications", userNotifications);
        }

        [HttpGet]
        public IActionResult Create()
        {
            List<UsersViewModel> usersList = new List<UsersViewModel>();
            HttpResponseMessage usersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;

            if (usersResponse.IsSuccessStatusCode)
            {
                string usersData = usersResponse.Content.ReadAsStringAsync().Result;
                usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            var model = new NotificationsViewModel
            {
                Users = usersList  // Ensure this is being set correctly
            };

            return View(model);
        }


        [HttpPost]
        public IActionResult Create(NotificationsViewModel model)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("UserId");
                model.CreatedBy = currentUserId; // Set the admin's ID

                if (model.Recipient == -1) // Check if "Të gjithve" was selected
                {
                    // Get all users
                    HttpResponseMessage usersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;
                    if (usersResponse.IsSuccessStatusCode)
                    {
                        string usersData = usersResponse.Content.ReadAsStringAsync().Result;
                        var usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);

                        // Send notification to each user
                        foreach (var user in usersList)
                        {
                            var individualNotification = new NotificationsViewModel
                            {
                                Message = model.Message,
                                Recipient = user.Id, // Set each user's ID as recipient
                                Type = model.Type,
                                Date = DateTime.Now
                            };

                            string data = JsonConvert.SerializeObject(individualNotification);
                            StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                            _client.PostAsync(_client.BaseAddress + "/Notifications/CreateNotification", content).Wait();
                        }

                        TempData["successMessage"] = "Notification sent to all users.";
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    // Handle single recipient notification
                    string data = JsonConvert.SerializeObject(model);
                    StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = _client.PostAsync(_client.BaseAddress + "/Notifications/CreateNotification", content).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        TempData["successMessage"] = "Notification Created.";
                        return RedirectToAction("Index");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
            }

            // Reload users for the dropdown list if there's an error
            HttpResponseMessage reloadUsersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;
            if (reloadUsersResponse.IsSuccessStatusCode)
            {
                string usersData = reloadUsersResponse.Content.ReadAsStringAsync().Result;
                model.Users = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
            }

            return View(model); // Pass the model back to the view
        }


        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                NotificationsViewModel notification = new NotificationsViewModel();

                // Fetch the specific notification
                HttpResponseMessage response = _client.GetAsync(_client.BaseAddress + "/Notifications/GetNotification" + id).Result;
                if (response.IsSuccessStatusCode)
                {
                    string data = response.Content.ReadAsStringAsync().Result;
                    notification = JsonConvert.DeserializeObject<NotificationsViewModel>(data);
                }

                // Fetch users to populate the dropdown list
                HttpResponseMessage usersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;
                if (usersResponse.IsSuccessStatusCode)
                {
                    string usersData = usersResponse.Content.ReadAsStringAsync().Result;
                    notification.Users = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
                }

                return View(notification);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public IActionResult Edit(NotificationsViewModel model)
        {
            try
            {
                string data = JsonConvert.SerializeObject(model);
                StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
                HttpResponseMessage response = _client.PutAsync(_client.BaseAddress + "/Notifications/UpdateNotification/" + model.Id, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "notification details updated. ";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {

                TempData["errorMessage"] = ex.Message;
                return View();
            }


            return View(); // Pass the model back to the view
        }


        [HttpPost]
        public IActionResult MarkAsRead(int id)
        {
            try
            {
                HttpResponseMessage response = _client.PutAsync($"{_client.BaseAddress}/Notifications/MarkAsRead/{id}/read", null).Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Notification marked as read.";
                }
                else
                {
                    TempData["errorMessage"] = "Failed to mark notification as read.";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = "An error occurred: " + ex.Message;
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        public IActionResult MarkAllAsRead(int userId)
        {
            try
            {
                // Call the API to mark all notifications as read for the specified user ID
                HttpResponseMessage response = _client.PutAsync($"{_client.BaseAddress}/Notifications/MarkAllAsRead/{userId}/markallread", null).Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "All notifications marked as read.";
                }
                else
                {
                    TempData["errorMessage"] = "Failed to mark all notifications as read.";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = "An error occurred: " + ex.Message;
            }

            return RedirectToAction("Index");
        }



        [HttpGet]
        public IActionResult Delete(string id)
        {
            try
            {
                NotificationsViewModel notification = new NotificationsViewModel();
                HttpResponseMessage response = _client.GetAsync(_client.BaseAddress + "/Notifications/GetNotification/" + id).Result;

                if (response.IsSuccessStatusCode)
                {
                    string data = response.Content.ReadAsStringAsync().Result;
                    notification = JsonConvert.DeserializeObject<NotificationsViewModel>(data);

                    HttpResponseMessage usersResponse = _client.GetAsync(_client.BaseAddress + "/Users/GetUsers").Result;
                    List<UsersViewModel> usersList = new List<UsersViewModel>();

                    if (usersResponse.IsSuccessStatusCode)
                    {
                        string usersData = usersResponse.Content.ReadAsStringAsync().Result;
                        usersList = JsonConvert.DeserializeObject<List<UsersViewModel>>(usersData);
                    }

                    var assignedUser = usersList.FirstOrDefault(u => u.Id == notification.Recipient);
                    notification.RecipientName = assignedUser != null ? assignedUser.FullName : "Unassigned";
                }

                return View(notification);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = ex.Message;
                return View();
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                HttpResponseMessage response = _client.DeleteAsync(_client.BaseAddress + "/Notifications/DeleteNotification/" + id).Result;
                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Notifications details deleted.";
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

        [HttpPost]
        public IActionResult DeleteAllNotifications(int id)
        {
            try
            {
                HttpResponseMessage response = _client.DeleteAsync($"{_client.BaseAddress}/Notifications/DeleteAllNotifications/user/{id}/deleteall").Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "All notifications for the user were deleted.";
                }
                else
                {
                    TempData["errorMessage"] = "Failed to delete all notifications for the user.";
                }
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = "An error occurred: " + ex.Message;

            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult GetUnreadNotificationCount()
        {
            // Retrieve the current user's ID from the session
            string username = HttpContext.Session.GetString("Username");

            if (username == null)
            {
                return RedirectToAction("Login", "Home"); // Redirect to login if user ID is not found
            }

            // Fetch the user to get the ID based on the username
            HttpResponseMessage userResponse = _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers").Result;
            List<UsersDTO> users = new List<UsersDTO>();

            if (userResponse.IsSuccessStatusCode)
            {
                string userData = userResponse.Content.ReadAsStringAsync().Result;
                users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);
            }

            // Find the current user's ID
            var currentUser = users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Use async API calls to get the unread notification count
            HttpResponseMessage response = _client.GetAsync($"{_client.BaseAddress}/Notifications/GetUnreadNotificationCount/user/{currentUser.Id}/unreadcount").Result;

            if (response.IsSuccessStatusCode)
            {
                string data = response.Content.ReadAsStringAsync().Result;
                var unreadCountResult = JsonConvert.DeserializeObject<dynamic>(data);
                return Content(unreadCountResult.unreadCount.ToString()); // Return the unread count as plain text
            }
            else
            {
                return Content("0"); // Return 0 if an error occurs
            }
        }

    }
}