using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace CustomerPortalAPI.Models
{
	public class Auth0User
	{
        [JsonPropertyName("sub")]
        public string UserId { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("picture")]
        public string Picture { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }

        // Custom namespace property for roles
        [JsonPropertyName("https://tech-brandingarc.us.auth0.comroles")]
        public List<string> Roles { get; set; }
    }
}

