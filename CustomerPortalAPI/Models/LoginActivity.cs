using System.ComponentModel.DataAnnotations;

namespace CustomerPortalAPI.Models
{
    public class LoginActivity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Details { get; set; }

        [MaxLength(100)]
        public string? IpAddress { get; set; }

        [MaxLength(1024)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
