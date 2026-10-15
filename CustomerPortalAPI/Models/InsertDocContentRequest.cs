namespace CustomerPortalAPI.Models;

public class InsertDocContentRequest
{
    public List<DeliverableFileModel> Posts { get; set; } = new();
}
