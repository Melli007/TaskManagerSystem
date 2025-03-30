using EtechTaskManager.Models;
using ETechTaskManager.Models;
using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System.Text;

namespace EtechTaskManager.Controllers
{
    public class MessagesController1 : Controller
    {
        private readonly HttpClient _client;
        Uri baseAddress = new Uri("https://localhost:7013/api");

        public MessagesController1()
        {
            _client = new HttpClient();
            _client.BaseAddress = baseAddress;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                List<MessagesViewModel> messagesList = new List<MessagesViewModel>();
                var response = _client.GetAsync($"{_client.BaseAddress}/Messages/GetAllMessages").Result;
                if (response.IsSuccessStatusCode)
                {
                    string data = response.Content.ReadAsStringAsync().Result;
                    messagesList = JsonConvert.DeserializeObject<List<MessagesViewModel>>(data);
                }
                else
                {
                    TempData["errorMessage"] = "Failed to load messages.";
                }
                return View(messagesList);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessagesByUser(int userId, int? currentContactId)
        {
            try
            {
                string username = HttpContext.Session.GetString("Username");
                if (username == null)
                {
                    return RedirectToAction("Login", "Home");

                }

                var userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                if (!userResponse.IsSuccessStatusCode)
                {
                    TempData["errorMessage"] = "Failed to load users.";
                    return View(new List<MessagesViewModel>());
                }

                var userData = await userResponse.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);

                var currentUser = users.FirstOrDefault(u => u.Username == username);
                if (currentUser == null)
                    return RedirectToAction("Login", "Home");

                ViewData["UserName"] = currentUser.Username;
                ViewData["UserId"] = currentUser.Id;
                ViewData["FullName"] = currentUser.FullName;

                var response = await _client.GetAsync($"{_client.BaseAddress}/Messages/GetMessagesByUser/{currentUser.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["errorMessage"] = "Failed to load user messages.";
                    return View(new List<MessagesViewModel>());
                }

                var data = await response.Content.ReadAsStringAsync();
                var userMessages = JsonConvert.DeserializeObject<List<MessagesViewModel>>(data);

                // Map SenderName, RecipientName, and Online Status
                foreach (var message in userMessages)
                {
                    var sender = users.FirstOrDefault(u => u.Id == message.SenderId);
                    var recipient = users.FirstOrDefault(u => u.Id == message.RecipientId);

                    message.SenderName = sender?.FullName ?? $"User {message.SenderId}";
                    message.SenderIsOnline = sender?.IsOnline ?? false; // Add IsOnline mapping
                    message.RecipientName = recipient?.FullName ?? $"User {message.RecipientId}";
                    message.RecipientIsOnline = recipient?.IsOnline ?? false; // Add IsOnline mapping

                    // Add unread count property
                    message.UnreadCount = userMessages.Count(m => m.SenderId == sender?.Id && !m.IsRead);
                }

                // Pass the currentContactId to ViewData
                ViewData["CurrentContactId"] = currentContactId ?? 0;

                return View(userMessages ?? new List<MessagesViewModel>());
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return View(new List<MessagesViewModel>());
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetConversation(int contactId)
        {
            try
            {
                string username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { error = "Not logged in" });
                }

                var userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                if (!userResponse.IsSuccessStatusCode)
                {
                    return Json(new { error = "Failed to load users." });
                }

                var userData = await userResponse.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);

                var currentUser = users.FirstOrDefault(u => u.Username == username);
                if (currentUser == null)
                {
                    return Json(new { error = "User not found." });
                }

                var convResponse = await _client.GetAsync(
                    $"{_client.BaseAddress}/Messages/GetMessagesBetweenUsers?senderId={currentUser.Id}&recipientId={contactId}"
                );
                if (!convResponse.IsSuccessStatusCode)
                {
                    return Json(new { error = "Failed to load conversation." });
                }

                var convData = await convResponse.Content.ReadAsStringAsync();
                var conversation = JsonConvert.DeserializeObject<List<MessagesViewModel>>(convData)
                      ?? new List<MessagesViewModel>();

                // Transform from "MID" -> "id"
                var transformed = conversation.Select(m => new {
                    id = m.MID,           // *** rename here ***
                    senderId = m.SenderId,
                    recipientId = m.RecipientId,
                    message = m.Message,
                    filePath = m.FilePath,
                    sentAt = m.SentAt,
                    isEdited = m.IsEdited, // Include this
                    isDeletedForEveryone = m.IsDeletedForEveryone,
                    isVisibleToSender = m.IsVisibleToSender,
                    isVisibleToRecipient = m.IsVisibleToRecipient
                    // readAt, isRead, etc. if needed
                }).ToList();

                return Json(transformed);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(MessagesViewModel model)
        {
            try
            {
                // Get the current user from Session
                string userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                {
                    return Json(new { success = false, message = "User is not logged in." });
                }

                model.SenderId = currentUserId;

                // Fallback if the message is blank but a file is uploaded
                if (string.IsNullOrWhiteSpace(model.Message) && model.File != null)
                {
                    model.Message = "File upload";
                }

                // Build the multipart form data
                using (var formData = new MultipartFormDataContent())
                {
                    formData.Add(new StringContent(model.SenderId.ToString()), "SenderId");
                    formData.Add(new StringContent(model.RecipientId.ToString()), "RecipientId");
                    formData.Add(new StringContent(model.Message ?? string.Empty), "Message");

                    if (model.File != null && model.File.Length > 0)
                    {
                        var fileContent = new StreamContent(model.File.OpenReadStream());
                        fileContent.Headers.ContentType =
                            new System.Net.Http.Headers.MediaTypeHeaderValue(model.File.ContentType);

                        formData.Add(fileContent, "File", model.File.FileName);
                    }

                    // Post to your API endpoint
                    var response = await _client.PostAsync($"{_client.BaseAddress}/Messages/CreateMessage", formData);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorMsg = await response.Content.ReadAsStringAsync();
                        return Json(new { success = false, message = $"Failed to create message: {errorMsg}" });
                    }
                }

                // Return success without reloading or redirecting
                return Json(new { success = true, message = "Message sent successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }



        // ... (Index, Delete, etc.) ...
        [HttpGet]
        public IActionResult Delete(int messageId)
        {
            try
            {
                MessagesViewModel message = new MessagesViewModel();
                HttpResponseMessage response = _client.GetAsync($"{_client.BaseAddress}/Messages/GetMessageById/{messageId}").Result;

                if (response.IsSuccessStatusCode)
                {
                    string data = response.Content.ReadAsStringAsync().Result;
                    message = JsonConvert.DeserializeObject<MessagesViewModel>(data);
                }

                return View(message);
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int messageId)
        {
            try
            {
                HttpResponseMessage response = _client.DeleteAsync($"{_client.BaseAddress}/Messages/DeleteMessage/{messageId}").Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Message deleted successfully.";
                    return RedirectToAction("Index");
                }

                TempData["errorMessage"] = "Failed to delete message.";
                return RedirectToAction("Delete", new { messageId });
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPut]
        public IActionResult MarkAsRead(int messageId)
        {
            try
            {
                HttpResponseMessage response = _client.PutAsync($"{_client.BaseAddress}/Messages/MarkMessageAsRead/{messageId}", null).Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "Message marked as read.";
                    return RedirectToAction("Index");
                }

                TempData["errorMessage"] = "Failed to mark message as read.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPut]
        public IActionResult MarkAllAsRead(int userId)
        {
            try
            {
                HttpResponseMessage response = _client.PutAsync($"{_client.BaseAddress}/Messages/MarkAllMessagesAsRead/{userId}", null).Result;

                if (response.IsSuccessStatusCode)
                {
                    TempData["successMessage"] = "All messages marked as read.";
                    return RedirectToAction("Index");
                }

                TempData["errorMessage"] = "Failed to mark all messages as read.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["errorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPut]
        public async Task<IActionResult> MarkMessagesAsRead(int userId, int contactId)
        {
            try
            {
                var response = await _client.PutAsync(
                    $"{_client.BaseAddress}/Messages/MarkMessagesAsReadBetweenUsers?userId={userId}&contactId={contactId}",
                    null
                );

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to mark messages as read.");
                }

                return Ok("Messages marked as read.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query, int page = 1, int pageSize = 10)
        {
            if (string.IsNullOrEmpty(query))
                return BadRequest("Search query cannot be empty.");

            var response = await _client.GetAsync(
                $"{_client.BaseAddress}/Messages/SearchUsers?query={query}&page={page}&pageSize={pageSize}"
            );

            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Failed to search users in API.");

            var data = await response.Content.ReadAsStringAsync();

            // If the API returns JSON array of users, just forward it back as JSON:
            var users = JsonConvert.DeserializeObject<List<UsersDTO>>(data);

            // Return JSON or return Ok(...) 
            return Ok(users);
        }


        [HttpGet]
        public async Task<IActionResult> SearchMessages(string query, int page = 1, int pageSize = 10)
        {
            try
            {
                // Get current user ID from session
                string userIdStr = HttpContext.Session.GetString("UserId");
                int.TryParse(userIdStr, out var currentUserId);

                // Call API to fetch filtered messages
                var response = await _client.GetAsync(
                    $"{_client.BaseAddress}/Messages/SearchMessages?query={query}&userId={currentUserId}&page={page}&pageSize={pageSize}");
                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, "Failed to search messages in API.");

                var data = await response.Content.ReadAsStringAsync();
                var messages = JsonConvert.DeserializeObject<List<MessagesViewModel>>(data) ?? new();

                // Fetch users to enrich sender/recipient details
                var userResponse = await _client.GetAsync($"{_client.BaseAddress}/Users/GetUsers");
                if (!userResponse.IsSuccessStatusCode)
                    return StatusCode(500, "Failed to load user list for search results.");

                var userData = await userResponse.Content.ReadAsStringAsync();
                var allUsers = JsonConvert.DeserializeObject<List<UsersDTO>>(userData);

                foreach (var m in messages)
                {
                    var sender = allUsers.FirstOrDefault(u => u.Id == m.SenderId);
                    var recipient = allUsers.FirstOrDefault(u => u.Id == m.RecipientId);

                    m.SenderName = sender?.FullName ?? sender?.Username ?? $"User {m.SenderId}";
                    m.RecipientName = recipient?.FullName ?? recipient?.Username ?? $"User {m.RecipientId}";
                    m.SenderIsOnline = sender?.IsOnline ?? false;
                    m.RecipientIsOnline = recipient?.IsOnline ?? false;
                }

                var transformed = messages.Select(m => new
                {
                    id = m.MID,
                    message = m.Message,
                    sentAt = m.SentAt,
                    contactId = m.SenderId == currentUserId ? m.RecipientId : m.SenderId,
                    contactName = m.SenderId == currentUserId ? m.RecipientName : m.SenderName,
                    isOnline = m.SenderId == currentUserId ? m.RecipientIsOnline : m.SenderIsOnline
                }).ToList();

                return Ok(transformed);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        [HttpPatch]
        public async Task<IActionResult> EditMessage(int messageId, string newText)
        {
            try
            {
                // 1) Call your API in the backend, e.g. /api/Messages/EditMessage?messageId=xxx&newText=xxx
                string apiUrl = $"https://localhost:7013/api/Messages/EditMessage?messageId={messageId}&newText={Uri.EscapeDataString(newText)}";

                using var req = new HttpRequestMessage(HttpMethod.Patch, apiUrl);
                // If your API expects JSON body, do that. Otherwise, from query is fine:
                // e.g. var body = new { MessageId = messageId, NewText = newText };
                // req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                var response = await _client.SendAsync(req);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Failed to edit: {error}");
                }

                return Ok("Edited in DB successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Exception: {ex.Message}");
            }
        }

        [HttpPatch]
        public async Task<IActionResult> DeleteForMe(int messageId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var currentUserId))
                return BadRequest("Not logged in.");

            string apiUrl = $"{_client.BaseAddress}/Messages/DeleteForMe?messageId={messageId}&userId={currentUserId}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, apiUrl);
            var response = await _client.SendAsync(req);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Failed to delete for me: {errorBody}");
            }

            return Ok("Message hidden for current user only.");
        }


        [HttpPatch]
        public async Task<IActionResult> DeleteForEveryone(int messageId)
        {
            try
            {
                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var currentUserId))
                    return BadRequest("Not logged in.");

                // 1) Construct the API URL
                string apiUrl = $"{_client.BaseAddress}/Messages/DeleteForEveryone?messageId={messageId}&userId={currentUserId}";

                // 2) Make a PATCH call to the API
                using var req = new HttpRequestMessage(HttpMethod.Patch, apiUrl);
                HttpResponseMessage response = await _client.SendAsync(req);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode,
                        $"Failed to delete for everyone: {errorBody}");
                }

                return Ok("Message deleted for everyone successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPatch]
        public async Task<IActionResult> DeleteChatForMe(int contactId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var currentUserId))
                return BadRequest("Not logged in.");

            string apiUrl = $"{_client.BaseAddress}/Messages/DeleteChatForMe/{currentUserId}/{contactId}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, apiUrl);
            var response = await _client.SendAsync(req);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Failed to delete chat: {errorBody}");
            }

            return Ok("Chat deleted for current user successfully.");
        }

        [HttpGet]
        public async Task<IActionResult> GetLastMessageForContact(int contactId)
        {
            try
            {
                // Retrieve the current user's ID from the session
                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var currentUserId))
                {
                    return Json(new { error = "User not logged in." });
                }

                // API URL to get the last message between the current user and the contact
                var apiUrl = $"{_client.BaseAddress}/Messages/GetLastMessageForContact/{currentUserId}/{contactId}";

                // Call the API
                var response = await _client.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { error = $"Failed to get the last message: {error}" });
                }

                // Deserialize the response
                var message = JsonConvert.DeserializeObject<MessagesViewModel>(await response.Content.ReadAsStringAsync());

                // Return the message details as JSON (can also return a View if needed)
                return Json(message);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCounts()
        {
            try
            {
                // Get the current user ID from the session
                string userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                {
                    return Json(new { success = false, message = "User is not logged in." });
                }

                // Call the API endpoint
                var response = await _client.GetAsync($"{_client.BaseAddress}/Messages/NotifyUnreadCount/{currentUserId}");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Failed to fetch unread counts" });
                }

                // Expecting: { success: true, data: [...] }
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

    }
}