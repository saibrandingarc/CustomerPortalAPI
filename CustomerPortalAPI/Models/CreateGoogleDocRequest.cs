namespace CustomerPortalAPI.Models;

public class CreateGoogleDocRequest
{
    public string DocTitle { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;
}
