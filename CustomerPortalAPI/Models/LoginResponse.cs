using System;
using System.ComponentModel.DataAnnotations;

namespace CustomerPortalAPI.Models
{
	public class LoginResponse
	{
        public string access_token { get; set; }
        public string id_token { get; set; }
        public string scope { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public List<string> Role { get; set; }
        public DateTime ExpiresIn { get; set; }
        public bool Auth0Status { get; set; }
        public string? Auth0AccountId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyId { get; set; }

    }
}

