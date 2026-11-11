using System.ComponentModel.DataAnnotations;

namespace CustomerPortalAPI.Models
{
    public class DeliverableFileApproval
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string DeliverableId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileId { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(2048)]
        public string? FileUrl { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }
    }
}
