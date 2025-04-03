using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Models;
using EtechTaskManagerBackend.Repository;
using EtechTaskManagerBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Net;
using SkiaSharp;

namespace EtechTaskManagerBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UsersController : ControllerBase // Changed to ControllerBase
    {
        private readonly IUsersRepository _usersRepository;
        private readonly LoggingService _loggingService;

        public UsersController(IUsersRepository usersRepository, LoggingService loggingService)
        {
            _usersRepository = usersRepository;
            _loggingService = loggingService;  // Inject the LoggingService
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Users>))]
        public IActionResult GetUsers()
        {
            var users = _usersRepository.GetUsers();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            //Krijimi i nje objeti qe bojm "Mapping" nqato atribute qe deshirojm mi paraqit ne metodenGet
            var userDto = users.Select(user => new UsersDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                Password = user.Password,
                Phone = user.Phone,
                Role = user.Role,
                Profession = user.Profession,
                CreatedAt = user.CreatedAt,
                Banned = user.Banned,
                ProfilePicturePath = user.ProfilePicturePath,
                IsOnline = user.IsOnline
            });

            return Ok(userDto); //Paraqitja e atyre atributeve
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Users))]
        [ProducesResponseType(400)]
        public IActionResult GetUserById(int id)
        {
            if (!_usersRepository.UserExists(id)) // Assuming UserExists method is implemented
            {
                return NotFound();
            }

            var user = _usersRepository.GetUserById(id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            //Mapping Userat ne UsersDTO per kthimin e atributeve qe deshirojm ti shfaqim
            var userDto = new UsersDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                Password = user.Password,
                Phone = user.Phone,
                Role = user.Role,
                Profession = user.Profession,
                CreatedAt = user.CreatedAt,
                Banned = user.Banned,
                IsOnline = user.IsOnline,
                ProfilePicturePath = user.ProfilePicturePath
            };

            return Ok(userDto); //Kthimi i DTO's
        }

        [HttpPost]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult CreateUsers([FromBody] UsersDTO usersCreate)
        {
            if (usersCreate == null)
            {
                return BadRequest(ModelState);
            }

            var users = _usersRepository.GetUsers().Where(c => c.FullName.Trim().ToUpper() == usersCreate.FullName.TrimEnd().ToUpper()).FirstOrDefault();

            if (users != null)
            {
                ModelState.AddModelError("", "User alrady exists");
                return StatusCode(422, ModelState);
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Manual mapping from UsersDTO to Users
            var usersMap = new Users
            {
                Id = usersCreate.Id,
                FullName = usersCreate.FullName,
                Username = usersCreate.Username,
                Password = usersCreate.Password,
                Role = usersCreate.Role,
                Profession = usersCreate.Profession,
                CreatedAt = DateTime.Now,
                Banned = usersCreate.Banned
            };

            _loggingService.LogUserDetails(usersMap);

            usersMap.Password = new PasswordHasher<Users>().HashPassword(usersMap, usersCreate.Password);


            if (!_usersRepository.CreateUsers(usersMap))
            {
                ModelState.AddModelError("", "Something went wrong while saving");
                return StatusCode(500, ModelState);
            }
            return Ok("Successfully created");

        }

        // PUT: api/users/{id}
        [HttpPut("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult UpdateUsers(int id, [FromBody] UsersDTO updatedUsers)
        {
            if (updatedUsers == null)
            {
                return BadRequest(ModelState);
            }


            if (!_usersRepository.UserExists(id))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return NotFound();
            }

            var userssMap = new Users
            {
                Id = updatedUsers.Id,
                FullName = updatedUsers.FullName,
                Username = updatedUsers.Username,
                Email = updatedUsers.Email,
                Password = updatedUsers.Password,
                Phone = updatedUsers.Phone,
                Role = updatedUsers.Role,
                Profession = updatedUsers.Profession,
                CreatedAt = updatedUsers.CreatedAt,
                Banned = updatedUsers.Banned
            };

            _loggingService.LogUserDetails(userssMap);

            userssMap.Password = new PasswordHasher<Users>().HashPassword(userssMap, updatedUsers.Password);

            if (!_usersRepository.UpdateUsers(userssMap))
            {
                ModelState.AddModelError("", "Something went wrong while saving");
                return StatusCode(500, ModelState);
            }
            return NoContent();
        }

        // PUT: api/users/{id}
        [HttpPut("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult UpdateUsersInfo(int id, [FromBody] UpdateUserDto updatedUsers)
        {
            if (updatedUsers == null)
            {
                return BadRequest(ModelState);
            }

            if (!_usersRepository.UserExists(id))
            {
                return NotFound();
            }

            // Check if the user is banned
            var existingUser = _usersRepository.GetUserById(id);
            if (existingUser.Banned)
            {
                return BadRequest("Banned users cannot update their information.");
            }


            // Update only the specified fields
            existingUser.Username = updatedUsers.Username ?? existingUser.Username;
            existingUser.Email = updatedUsers.Email ?? existingUser.Email;
            existingUser.Phone = updatedUsers.Phone ?? existingUser.Phone;

            _loggingService.LogUserDetails(existingUser);

            // Hash the password if it's provided
            if (!string.IsNullOrEmpty(updatedUsers.Password))
            {
                var passwordHasher = new PasswordHasher<Users>();
                existingUser.Password = passwordHasher.HashPassword(existingUser, updatedUsers.Password);
            }

            if (!_usersRepository.UpdateUsersInfo(existingUser))
            {
                return StatusCode(500, "Something went wrong while updating the user.");
            }

            return NoContent();
        }

        [HttpPost("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public IActionResult UpdatePassword(int id, [FromBody] PasswordUpdateDto passwordDto)
        {
            if (passwordDto == null)
            {
                return BadRequest("Invalid password data.");
            }

            var user = _usersRepository.GetUserById(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Check if the user is banned
            if (user.Banned)
            {
                return BadRequest("Banned users cannot update their password.");
            }

            // Verify the old password
            var passwordHasher = new PasswordHasher<Users>();
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, passwordDto.OldPassword);

            if (verificationResult != PasswordVerificationResult.Success)
            {
                return BadRequest("Old password is incorrect.");
            }

            // Validate the new password
            if (!IsPasswordValid(passwordDto.NewPassword))
            {
                return BadRequest("New password must be at least 12 characters long and contain at least 1 digit, 1 uppercase letter, and 1 lowercase letter.");
            }

            // Update the password
            user.Password = passwordHasher.HashPassword(user, passwordDto.NewPassword);

            if (!_usersRepository.UpdateUsers(user))
            {
                return StatusCode(500, "Failed to update the password.");
            }

            return Ok("Password updated successfully.");
        }

        // Password validation helper method
        private bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12)
            {
                return false;
            }

            // Check for required character types
            bool hasUppercase = password.Any(char.IsUpper);
            bool hasLowercase = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);

            return hasUppercase && hasLowercase && hasDigit;
        }

        // Delete: api/users/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(400)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult DeleteUser(int id)
        {
            if (!_usersRepository.UserExists(id))
            {
                return NotFound();
            }

            var userToDelete = _usersRepository.GetUserById(id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_usersRepository.DeleteUser(userToDelete))
            {
                ModelState.AddModelError("", "Something went wrong deleting user");
            }

            // Log the deletion with anonymized details
            _loggingService.LogUserDetails(new Users
            {
                Id = userToDelete.Id,
                FullName = "Deleted",
                Username = "Deleted",
                Email = "Deleted",
                Password = null,
                Phone = "Deleted",
                Role = null,
                Profession = null,
                CreatedAt = userToDelete.CreatedAt,
                Banned = userToDelete.Banned
            });

            return NoContent();
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult BanUser(int id)
        {
            if (!_usersRepository.UserExists(id))
            {
                return NotFound("User not found.");
            }

            if (!_usersRepository.UpdateUserBannedStatus(id, true))
            {
                return StatusCode(500, "Failed to ban the user.");
            }

            return Ok("User banned successfully.");
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult UnbanUser(int id)
        {
            if (!_usersRepository.UserExists(id))
            {
                return NotFound("User not found.");
            }

            if (!_usersRepository.UpdateUserBannedStatus(id, false))
            {
                return StatusCode(500, "Failed to unban the user.");
            }

            return Ok("User unbanned successfully.");
        }

        [HttpPut("{id}/online-status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult UpdateOnlineStatus(int id, [FromBody] bool isOnline)
        {
            if (!_usersRepository.UserExists(id))
            {
                return NotFound("User not found.");
            }

            if (!_usersRepository.UpdateUserOnlineStatus(id, isOnline))
            {
                return StatusCode(500, "Failed to update the online status.");
            }

            return Ok("User online status updated successfully.");
        }

        [HttpGet("online-users")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult GetOnlineUsers()
        {
            var users = _usersRepository.GetUsers().Where(u => u.IsOnline).ToList();

            if (!users.Any())
            {
                return NotFound("No users are currently online.");
            }

            var userDto = users.Select(user => new UsersDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Profession = user.Profession,
                CreatedAt = user.CreatedAt,
                Banned = user.Banned,
                IsOnline = user.IsOnline // Ensure the DTO includes IsOnline
            });

            return Ok(userDto);
        }
        [HttpPut]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public IActionResult SetAllOffline()
        {
            if (!_usersRepository.SetAllUsersOffline())
            {
                return StatusCode(500, "Failed to set all users offline.");
            }

            return Ok("All users have been set offline.");
        }

        // GET: api/Users/GetProfilePicturePath/5
        [HttpGet("{userId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult GetProfilePicturePath(int userId)
        {
            // Attempt to retrieve the path from the repo
            var path = _usersRepository.GetProfilePicturePath(userId);

            // If user or path is null, you could return 404
            if (path == null)
            {
                return NotFound("User not found or no profile picture set.");
            }

            // Otherwise return the path in JSON
            return Ok(new { ProfilePicturePath = path });
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UploadProfilePicture([FromForm] UploadProfilePictureDTO uploadProfilePicture)
        {
            // Validate user existence
            if (!_usersRepository.UserExists(uploadProfilePicture.UserId))
            {
                return NotFound("User not found.");
            }

            var file = uploadProfilePicture.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest("Invalid file.");
            }

            // Validate file extension and size
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest("Invalid file type. Only JPG, JPEG, and PNG are allowed.");
            }

            const long maxFileSize = 2 * 1024 * 1024; // 2MB
            if (file.Length > maxFileSize)
            {
                return BadRequest("File size exceeds the maximum limit of 2MB.");
            }

            // Validate file content
            try
            {
                if (!IsValidFileContent(file.OpenReadStream(), fileExtension))
                {
                    return BadRequest("Invalid image file.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error validating file content: {ex.Message}");
            }

            // Generate secure file name
            var sanitizedFileName = $"{Guid.NewGuid()}{fileExtension}";
            sanitizedFileName = string.Concat(sanitizedFileName.Split(Path.GetInvalidFileNameChars()));

            // Set up upload path
            var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Pictures", "Uploads", "ProfilePictures");
            Directory.CreateDirectory(uploadDirectory); // Ensure directory exists
            var filePath = Path.Combine(uploadDirectory, sanitizedFileName);

            try
            {
                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update user's profile picture path in the database
                var profilePicturePath = $"/Pictures/Uploads/ProfilePictures/{sanitizedFileName}";
                var fullUrl = $"https://localhost:7013{profilePicturePath}";
                if (!_usersRepository.UpdateProfilePicturePath(uploadProfilePicture.UserId, fullUrl))
                {
                    return StatusCode(500, "Failed to update profile picture in the database.");
                }

                Console.WriteLine(fullUrl);
                return Ok(new { Message = "Profile picture uploaded successfully.", ProfilePicturePath = fullUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error while saving the file: {ex.Message}");
            }
        }


        private bool IsValidFileContent(Stream fileStream, string fileExtension)
        {
            // Magic numbers for common image formats
            var magicNumbers = new Dictionary<string, byte[]>
            {
                { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } }, // PNG
                { ".jpg", new byte[] { 0xFF, 0xD8 } },             // JPG
                { ".jpeg", new byte[] { 0xFF, 0xD8 } }            // JPEG
            };

            if (magicNumbers.ContainsKey(fileExtension))
            {
                var expectedMagicNumber = magicNumbers[fileExtension];

                byte[] fileHeader = new byte[expectedMagicNumber.Length];
                fileStream.Position = 0; // Reset stream position
                fileStream.Read(fileHeader, 0, fileHeader.Length);
                fileStream.Position = 0; // Reset again after reading

                // Compare magic numbers
                return fileHeader.SequenceEqual(expectedMagicNumber);
            }

            return false; // Unsupported file extension
        }

        [HttpPost]
        public IActionResult SaveResetKey([FromBody] ResetKeyDTO model)
        {
            // Validate
            if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.ResetKey))
                return BadRequest("Invalid request body.");

            // Use your repository to save the reset key
            var success = _usersRepository.SaveResetKey(model.Email, model.ResetKey, DateTime.Now.AddMinutes(30));
            if (!success)
                return NotFound("User not found or unable to save reset key.");

            return Ok("Reset key saved successfully.");
        }

        [HttpPost]
        public IActionResult ResetPassword([FromBody] ResetPasswordDTO model)
        {
            if (model == null || string.IsNullOrEmpty(model.ResetKey) || string.IsNullOrEmpty(model.NewPassword))
                return BadRequest("Invalid request body.");

            // Find the user by the reset key
            var user = _usersRepository.GetUserByResetKey(model.ResetKey);
            if (user == null)
                return NotFound("Invalid or unknown reset key.");

            // Optional: check if the key is expired
            if (user.ResetKeyExpiry.HasValue && user.ResetKeyExpiry.Value < DateTime.Now)
                return BadRequest("Reset key is expired. Please request a new one.");

            // Hash the new password
            var passwordHasher = new PasswordHasher<Users>();
            user.Password = passwordHasher.HashPassword(user, model.NewPassword);

            // Clear the reset key
            user.ResetKey = null;
            user.ResetKeyExpiry = null;

            // Save
            if (!_usersRepository.UpdateUsers(user))
                return StatusCode(500, "Failed to update the password.");

            return Ok("Password has been reset successfully.");
        }

        [HttpPost]
        public IActionResult SendEmail([FromBody] EmailContent model)
        {
            // Basic validation
            if (string.IsNullOrEmpty(model.To) || string.IsNullOrEmpty(model.Subject))
                return BadRequest("Email 'To' and 'Subject' are required.");

            try
            {
                // 1) Configure SMTP for Gmail
                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    // Most critical: UseDefaultCredentials = false
                    smtp.UseDefaultCredentials = false;

                    // Replace with your Gmail address & app password
                    smtp.Credentials = new NetworkCredential("Putyourownemailhere@gmail.com", "gxyd eald cywh xgyc");
                    smtp.EnableSsl = true; // Gmail requires SSL/TLS

                    // 2) Build the MailMessage
                    var mail = new MailMessage
                    {
                        From = new MailAddress("Putyourownemailhere@gmail.com"),
                        Subject = model.Subject,
                        Body = model.Body,
                        IsBodyHtml = true // If sending HTML content
                    };
                    mail.To.Add(model.To);

                    // 3) Send it
                    smtp.Send(mail);
                }

                return Ok("Email sent successfully via Gmail.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to send email: {ex.Message}");
            }
        }

        public class EmailContent
        {
            public string To { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
        }
    }
}
