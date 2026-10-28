using System;
namespace CustomerPortalAPI.Models
{
	public class EmailDto
	{
        public string Subject { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime Date { get; set; }
        public string Body { get; set; }
    }
}

