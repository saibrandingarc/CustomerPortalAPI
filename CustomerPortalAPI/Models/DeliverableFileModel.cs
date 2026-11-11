namespace CustomerPortalAPI.Models;

public class DeliverableFileModel
{
    public string DeliverableId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? Hashtags { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string EditedText { get; set; } = string.Empty;
}
