using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class GoogleDriveController : ControllerBase
{
    private readonly GoogleServices _googleService;

    public GoogleDriveController(GoogleServices googleService)
    {
        _googleService = googleService;
    }

    [HttpGet("files")]
    public async Task<ActionResult<List<DriveItemDto>>> ListFiles()
    {
        var files = await _googleService.ListDriveFilesAsync();
        return Ok(files.Select(ToDriveItemDto).ToList());
    }

    [HttpGet("folders")]
    public async Task<ActionResult<List<DriveItemDto>>> ListFolders()
    {
        var folders = await _googleService.ListDriveFoldersAsync();
        return Ok(folders.Files?.Select(ToDriveItemDto).ToList() ?? new List<DriveItemDto>());
    }

    [HttpGet("folders/files")]
    public async Task<ActionResult<DriveFolderFilesResponse>> ListFilesInFolder([FromQuery] string folderUrl)
    {
        if (string.IsNullOrWhiteSpace(folderUrl))
        {
            return BadRequest(new { message = "folderUrl is required." });
        }

        var folderId = GoogleServices.ExtractFolderIdFromUrl(folderUrl);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return BadRequest(new { message = "Could not extract a folder ID from the provided URL." });
        }

        try
        {
            var files = await _googleService.ListDriveFilesByFolderUrlAsync(folderUrl);
            return Ok(new DriveFolderFilesResponse
            {
                FolderId = folderId,
                FolderUrl = $"https://drive.google.com/drive/folders/{folderId}",
                Files = files.Select(ToDriveItemDto).ToList()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("folders/files")]
    public async Task<ActionResult<DriveFolderFilesResponse>> ListFilesInFolderPost([FromBody] DriveFolderFilesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FolderUrl))
        {
            return BadRequest(new { message = "folderUrl is required." });
        }

        var folderId = GoogleServices.ExtractFolderIdFromUrl(request.FolderUrl);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return BadRequest(new { message = "Could not extract a folder ID from the provided URL." });
        }

        try
        {
            var files = await _googleService.ListDriveFilesByFolderUrlAsync(request.FolderUrl);
            return Ok(new DriveFolderFilesResponse
            {
                FolderId = folderId,
                FolderUrl = $"https://drive.google.com/drive/folders/{folderId}",
                Files = files.Select(ToDriveItemDto).ToList()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("folders/{folderName}/id")]
    public async Task<IActionResult> GetFolderId(string folderName)
    {
        var folderId = await _googleService.GetFolderIdByNameAsync(folderName);
        if (string.IsNullOrEmpty(folderId))
        {
            return NotFound(new { message = $"Folder '{folderName}' not found." });
        }

        return Ok(new { folderId, folderName });
    }

    [HttpPost("folders/ensure")]
    public async Task<IActionResult> EnsureFolder([FromQuery] string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return BadRequest(new { message = "folderName is required." });
        }

        var folderId = await _googleService.EnsureFolderExistsAsync(folderName);
        return Ok(new { folderId, folderName });
    }

    [HttpPost("docs")]
    public async Task<IActionResult> CreateDoc([FromBody] CreateGoogleDocRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocTitle) || string.IsNullOrWhiteSpace(request.FolderId))
        {
            return BadRequest(new { message = "DocTitle and FolderId are required." });
        }

        var fileId = await _googleService.CreateGoogleDocAsync(request.DocTitle, request.FolderId);
        return Ok(new
        {
            fileId,
            docUrl = $"https://docs.google.com/document/d/{fileId}/edit"
        });
    }

    [HttpPost("files/read")]
    public async Task<IActionResult> ReadFile([FromBody] GoogleFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GoogleFileId))
        {
            return BadRequest(new { message = "GoogleFileId is required." });
        }

        var content = await _googleService.ReadDriveFilesAsync(request);
        return Ok(new { content });
    }

    [HttpPost("docs/{fileId}/content")]
    public async Task<ActionResult<List<PostBookmarkInfo>>> InsertDocContent(string fileId, [FromBody] InsertDocContentRequest request)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return BadRequest(new { message = "fileId is required." });
        }

        if (request.Posts == null || request.Posts.Count == 0)
        {
            return BadRequest(new { message = "At least one post is required." });
        }

        var bookmarks = await _googleService.InsertContentToGoogleDocAsync(fileId, request.Posts);
        return Ok(bookmarks);
    }

    [HttpPost("docs/{documentId}/share")]
    public async Task<ActionResult<List<DrivePermissionDto>>> ShareDocument(string documentId, [FromBody] ShareDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return BadRequest(new { message = "documentId is required." });
        }

        if (request.EmailAddresses == null || request.EmailAddresses.Count == 0)
        {
            return BadRequest(new { message = "At least one email address is required." });
        }

        var permissions = await _googleService.ShareDocumentWithUsersAsync(documentId, request.EmailAddresses);
        return Ok(permissions.Select(p => new DrivePermissionDto
        {
            Id = p.Id,
            Type = p.Type,
            Role = p.Role,
            EmailAddress = p.EmailAddress
        }).ToList());
    }

    private static DriveItemDto ToDriveItemDto(Google.Apis.Drive.v3.Data.File file)
    {
        return new DriveItemDto
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            WebViewLink = file.WebViewLink,
            ModifiedTime = file.ModifiedTimeDateTimeOffset?.UtcDateTime
        };
    }
}
