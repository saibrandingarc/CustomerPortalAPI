using System;
namespace CustomerPortalAPI.Models
{
	public class EmailInboxModel
	{
        public string? MessageId { get; set; }
        public string? ThreadId { get; set; }
        public string? MessageUrl { get; set; }

        //// To details
        //public string[]? ToNames { get; set; }
        //public string[]? ToEmails { get; set; }

        //// From details
        public string? FromName { get; set; }
        public string? FromEmail { get; set; }

        public string? Subject { get; set; }

        // Body content
        public string? BodyHtml { get; set; }
        public string? BodyHtmlRgb { get; set; }
        public string? BodyPlain { get; set; }

        // Metadata
        public DateTime? Date { get; set; }
        public string? Label { get; set; }
    }
}

