using System;
namespace CustomerPortalAPI.Models
{
	public class CaseModel
	{
        public string? Id { get; set; }
        public Account Account_Name { get; set; }
        public string? Status { get; set; }
        public string? Email { get; set; }
        public string? Description { get; set; }
        public string? Internal_Comments { get; set; }
        public string? Priority { get; set; }
        public string? Reported_By { get; set; }
        public string? Case_Origin { get; set; }
        public string? Case_Reason { get; set; }
        public string? Subject { get; set; }
        public string? Type { get; set; }
        public string? Phone { get; set; }
        public List<EmailNote>? Email_Notes1 { get; set; }
    }

    public class Account
    {
        public string id { get; set; }
        // Add other Account properties if needed
    }

    public class EmailNote
    {
        public string? Comment_Type { get; set; }
        public string? Comments { get; set; }
        public DateTime? Comment_Date { get; set; }
    }

    public class ParentId
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }

    public class ZohoCaseRequest
    {
        public List<CaseModel> data { get; set; }
    }

    public class CaseContextRequest
    {
        public string? Comments { get; set; }
    }
}

