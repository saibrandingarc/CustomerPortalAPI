namespace CustomerPortalAPI.Models;

public class DriveItemDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? MimeType { get; set; }

    public string? WebViewLink { get; set; }

    public DateTime? ModifiedTime { get; set; }
}

public class DriveFolderFilesRequest
{
    public string FolderUrl { get; set; } = string.Empty;
}

public class DriveFolderFilesResponse
{
    public string FolderId { get; set; } = string.Empty;

    public string FolderUrl { get; set; } = string.Empty;

    public List<DriveItemDto> Files { get; set; } = new();
}

public class DrivePermissionDto
{
    public string? Id { get; set; }

    public string? Type { get; set; }

    public string? Role { get; set; }

    public string? EmailAddress { get; set; }
}
