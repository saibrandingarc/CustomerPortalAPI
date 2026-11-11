using System;
namespace CustomerPortalAPI.Models
{
	public class Staff
	{
        public int Id { get; set; }
        public string? StaffType { get; set; }
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
    }
}

