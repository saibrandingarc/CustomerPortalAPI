using System;
namespace CustomerPortalAPI.Models
{
	public class MainMenu
    {
        public int Id { get; set; }
        public string? MainContentType { get; set; }
        public string? Project { get; set; }
        public string? Description { get; set; }
        public string? Credits { get; set; }
        public string? PrimaryDistribution { get; set; }
        public string? OtherDistribution { get; set; }
        public string? TaskType { get; set; }
    }
}

