using System.ComponentModel.DataAnnotations;

namespace EtechTaskManagerBackend.DTO
{
    public class UploadProfilePictureDTO
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public IFormFile File { get; set; }
    }
}
