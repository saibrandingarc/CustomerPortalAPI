using System;
using System.ComponentModel.DataAnnotations;

namespace CustomerPortalAPI.Models
{
	public class User
	{
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string? Email { get; set; }

        public string? Password { get; set; }

        [MaxLength(6)]
        public string? Otp { get; set; }

        public bool ZohoEmailStatus { get; set; }

        public bool OTPStatus { get; set; }

        public bool Auth0Status { get; set; }

        [MaxLength(255)]
        public string? Auth0AccountId { get; set; }

        [MaxLength(255)]
        public string? CompanyName { get; set; }

        [MaxLength(255)]
        public string? CompanyId { get; set; }

        public string? full_name { get; set; }
        public string? first_name { get; set; }
        public string? auth0role { get; set; }
    }
}
