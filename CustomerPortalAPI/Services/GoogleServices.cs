using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CustomerPortalAPI.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Document = Google.Apis.Docs.v1.Data.Document;
using Link = Google.Apis.Docs.v1.Data.Link;
using Request = Google.Apis.Docs.v1.Data.Request;

namespace CustomerPortalAPI.Services;

public class GoogleServices
{
    private class TextSegment
    {
        public string CleanText { get; set; } = string.Empty;

        public List<(int Start, int End)> BoldRanges { get; set; } = new();
    }

    private const string ServiceAccountFileName = "badrive-baef0ef44a19.json";

    private readonly IConfiguration _configuration;
    private readonly DriveService _driveService;
    private readonly DocsService _docsService;

    public GoogleServices(IConfiguration configuration)
    {
        _configuration = configuration;
        var credential = LoadCredential(
            DriveService.Scope.Drive,
            DocsService.Scope.Documents);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BADrive"
        });
        _docsService = new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BADrive"
        });
    }

    private static string GetServiceAccountPath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), ServiceAccountFileName);
    }

    private static GoogleCredential LoadCredential(params string[] scopes)
    {
        var fullPath = GetServiceAccountPath();
        if (!System.IO.File.Exists(fullPath))
        {
            throw new FileNotFoundException("Service account file not found at " + fullPath);
        }

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return GoogleCredential.FromStream(stream).CreateScoped(scopes);
    }

    private static DriveService CreateReadOnlyDriveService()
    {
        var credential = LoadCredential(DriveService.Scope.DriveReadonly);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BADrive"
        });
    }

    public async Task<IList<Google.Apis.Drive.v3.Data.File>> ListDriveFilesAsync()
    {
        var service = CreateReadOnlyDriveService();

        var folderListRequest = service.Files.List();
        folderListRequest.Q = "mimeType = 'application/vnd.google-apps.folder'";
        folderListRequest.Fields = "files(id, name)";
        foreach (var folder in (await folderListRequest.ExecuteAsync()).Files)
        {
            Console.WriteLine("Folder Name: " + folder.Name + ", ID: " + folder.Id);
        }

        const string folderId = "1koengU0pcVdPAAqHT1vOXcejuwVjsjMU";
        var listRequest = service.Files.List();
        listRequest.Q = "'" + folderId + "' in parents and trashed = false";
        listRequest.Fields = "files(id, name, mimeType)";
        var files = await listRequest.ExecuteAsync();
        foreach (var file in files.Files)
        {
            Console.WriteLine($"File Name: {file.Name}, ID: {file.Id}, Type: {file.MimeType}");
        }

        return files.Files;
    }

    public async Task<FileList> ListDriveFoldersAsync()
    {
        var service = CreateReadOnlyDriveService();
        var listRequest = service.Files.List();
        listRequest.Q = "mimeType = 'application/vnd.google-apps.folder'";
        listRequest.Fields = "files(id, name)";
        return await listRequest.ExecuteAsync();
    }

    public static string? ExtractFolderIdFromUrl(string folderUrlOrId)
    {
        if (string.IsNullOrWhiteSpace(folderUrlOrId))
        {
            return null;
        }

        var value = folderUrlOrId.Trim();

        if (!value.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var folderMatch = Regex.Match(value, @"/folders/([a-zA-Z0-9_-]+)");
        if (folderMatch.Success)
        {
            return folderMatch.Groups[1].Value;
        }

        var idMatch = Regex.Match(value, @"[?&]id=([a-zA-Z0-9_-]+)");
        if (idMatch.Success)
        {
            return idMatch.Groups[1].Value;
        }

        return null;
    }

    public async Task<IList<Google.Apis.Drive.v3.Data.File>> ListDriveFilesByFolderUrlAsync(string folderUrlOrId)
    {
        var folderId = ExtractFolderIdFromUrl(folderUrlOrId);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw new ArgumentException("Could not extract a folder ID from the provided URL or ID.");
        }

        var service = CreateReadOnlyDriveService();
        var listRequest = service.Files.List();
        listRequest.Q = "'" + folderId + "' in parents and trashed = false";
        listRequest.Fields = "files(id, name, mimeType, webViewLink, modifiedTime)";
        listRequest.OrderBy = "folder,name";
        listRequest.SupportsAllDrives = true;
        listRequest.IncludeItemsFromAllDrives = true;

        var files = await listRequest.ExecuteAsync();
        return files.Files ?? new List<Google.Apis.Drive.v3.Data.File>();
    }

    public async Task<string?> GetFolderIdByNameAsync(string folderName)
    {
        var driveFolderName = folderName switch
        {
            "Evergreen Content" => _configuration["FolderNames:Evergreen"],
            "Holiday & Seasonal" => _configuration["FolderNames:HolidayPosts"],
            _ => _configuration["FolderNames:Default"]
        };

        var service = CreateReadOnlyDriveService();
        var listRequest = service.Files.List();
        listRequest.Q = "mimeType='application/vnd.google-apps.folder' and name='" + driveFolderName + "'";
        listRequest.Fields = "files(id, name)";
        return (await listRequest.ExecuteAsync()).Files.FirstOrDefault()?.Id;
    }

    public async Task<string> EnsureFolderExistsAsync(string folderName)
    {
        var listRequest = _driveService.Files.List();
        listRequest.Q = "mimeType='application/vnd.google-apps.folder' and name='" + folderName + "' and trashed=false";
        listRequest.Fields = "files(id, name)";
        var folder = (await listRequest.ExecuteAsync()).Files.FirstOrDefault();
        if (folder != null)
        {
            return folder.Id;
        }

        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = "MyDocument",
            MimeType = "application/vnd.google-apps.document"
        };
        var createRequest = _driveService.Files.Create(folderMetadata);
        createRequest.Fields = "id";
        return (await createRequest.ExecuteAsync()).Id;
    }

    public async Task<string> CreateGoogleDocAsync(string docTitle, string folderId)
    {
        var escapedDocTitle = docTitle.Replace("'", "\\'");
        var query = $"name = '{escapedDocTitle}' and mimeType = 'application/vnd.google-apps.document' and '{folderId}' in parents and trashed = false";
        var listRequest = _driveService.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";
        var files = await listRequest.ExecuteAsync();

        string fileId;
        if (files.Files != null && files.Files.Count > 0)
        {
            fileId = files.Files[0].Id;
        }
        else
        {
            var doc = new Document { Title = docTitle };
            fileId = (await _docsService.Documents.Create(doc).ExecuteAsync()).DocumentId;
            var file = await _driveService.Files.Get(fileId).ExecuteAsync();
            var previousParents = file.Parents != null ? string.Join(",", file.Parents) : "";
            var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
            updateRequest.AddParents = folderId;
            if (!string.IsNullOrEmpty(previousParents))
            {
                updateRequest.RemoveParents = previousParents;
            }
            updateRequest.Fields = "id, parents";
            await updateRequest.ExecuteAsync();
        }

        return fileId;
    }

    public async Task<string> ReadDriveFilesAsync(GoogleFileRequest googleFileRequest)
    {
        var service = CreateReadOnlyDriveService();
        var request = service.Files.Get(googleFileRequest.GoogleFileId);
        var memoryStream = new MemoryStream();
        await request.DownloadAsync(memoryStream);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);

        var pathParts = new List<string>();
        var currentFileId = googleFileRequest.GoogleFileId;

        while (!string.IsNullOrEmpty(currentFileId))
        {
            var metaRequest = service.Files.Get(currentFileId);
            metaRequest.Fields = "id, name, parents";
            var fileMeta = await metaRequest.ExecuteAsync();
            pathParts.Insert(0, fileMeta.Name);

            if (fileMeta.Parents == null || fileMeta.Parents.Count == 0)
            {
                break;
            }

            currentFileId = fileMeta.Parents[0];
        }

        var drivePath = string.Join("/", pathParts);
        Console.WriteLine("drive path :" + drivePath);
        return await reader.ReadToEndAsync();
    }

    public async Task<List<PostBookmarkInfo>> InsertContentToGoogleDocAsync(string fileId, List<DeliverableFileModel> posts)
    {
        var requests = new List<Request>();
        var bookmarkInfos = new List<PostBookmarkInfo>();
        var doc = _docsService.Documents.Get(fileId).Execute();
        var currentIndex = doc.Body.Content.Last().EndIndex.GetValueOrDefault(2) - 1;

        foreach (var post in posts)
        {
            var parsedBody = ParseMarkdownBold(post.Body);
            if (parsedBody == null)
            {
                continue;
            }

            var bookmarkName = "Bookmark_" + Guid.NewGuid().ToString("N");
            var titleText = post.Title + "\n\n";
            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = titleText,
                    Location = new Location { Index = currentIndex }
                }
            });
            requests.Add(new Request
            {
                CreateNamedRange = new CreateNamedRangeRequest
                {
                    Name = bookmarkName,
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = currentIndex,
                        EndIndex = currentIndex + titleText.Length
                    }
                }
            });
            requests.Add(new Request
            {
                UpdateTextStyle = new UpdateTextStyleRequest
                {
                    TextStyle = new TextStyle
                    {
                        Bold = true,
                        FontSize = new Dimension { Magnitude = 14.0, Unit = "PT" },
                        WeightedFontFamily = new WeightedFontFamily { FontFamily = "Cambria" }
                    },
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = currentIndex,
                        EndIndex = currentIndex + titleText.Length
                    },
                    Fields = "bold,fontSize,weightedFontFamily"
                }
            });
            currentIndex += titleText.Length;

            var bodyText = parsedBody.CleanText + "\n";
            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = bodyText,
                    Location = new Location { Index = currentIndex }
                }
            });
            requests.Add(new Request
            {
                UpdateTextStyle = new UpdateTextStyleRequest
                {
                    TextStyle = new TextStyle
                    {
                        FontSize = new Dimension { Magnitude = 12.0, Unit = "PT" },
                        WeightedFontFamily = new WeightedFontFamily { FontFamily = "Cambria" }
                    },
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = currentIndex,
                        EndIndex = currentIndex + bodyText.Length
                    },
                    Fields = "weightedFontFamily,fontSize"
                }
            });
            requests.Add(new Request
            {
                UpdateParagraphStyle = new UpdateParagraphStyleRequest
                {
                    ParagraphStyle = new ParagraphStyle { LineSpacing = 150f },
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = currentIndex,
                        EndIndex = currentIndex + bodyText.Length
                    },
                    Fields = "lineSpacing"
                }
            });

            foreach (var (start, end) in parsedBody.BoldRanges)
            {
                requests.Add(new Request
                {
                    UpdateTextStyle = new UpdateTextStyleRequest
                    {
                        TextStyle = new TextStyle
                        {
                            Bold = true,
                            FontSize = new Dimension { Magnitude = 12.0, Unit = "PT" },
                            WeightedFontFamily = new WeightedFontFamily { FontFamily = "Cambria" }
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = currentIndex + 1 + start,
                            EndIndex = currentIndex + 1 + end
                        },
                        Fields = "bold,fontSize,weightedFontFamily"
                    }
                });
            }

            currentIndex += bodyText.Length;

            var graphicsUrl = "";
            var graphicsUrlHead = "Graphics Link:\n";
            var graphicsUrlText = graphicsUrlHead + " " + graphicsUrl + "\n";
            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = graphicsUrlText,
                    Location = new Location { Index = currentIndex }
                }
            });
            var graphicsUrlStartIndex = currentIndex + graphicsUrlText.IndexOf(graphicsUrlHead);
            var graphicsUrlEndIndex = graphicsUrlStartIndex + graphicsUrlHead.Length;
            requests.Add(new Request
            {
                UpdateTextStyle = new UpdateTextStyleRequest
                {
                    TextStyle = new TextStyle
                    {
                        Bold = true,
                        FontSize = new Dimension { Magnitude = 12.0, Unit = "PT" },
                        WeightedFontFamily = new WeightedFontFamily { FontFamily = "Cambria" },
                        Link = new Link { Url = graphicsUrl }
                    },
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = graphicsUrlStartIndex,
                        EndIndex = graphicsUrlEndIndex
                    },
                    Fields = "bold,fontSize,weightedFontFamily"
                }
            });
            currentIndex += graphicsUrlText.Length;

            var deliverableUrl = "https://crm.zoho.com/crm/org668627929/tab/CustomModule22/" + post.DeliverableId;
            var deliverableUrlHead = "Deliverable Link\n";
            var deliverableUrlText = deliverableUrlHead + "\n";
            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = deliverableUrlText,
                    Location = new Location { Index = currentIndex }
                }
            });
            var urlStartIndex = currentIndex + deliverableUrlText.IndexOf(deliverableUrlHead);
            var urlEndIndex = urlStartIndex + deliverableUrlHead.Length;
            requests.Add(new Request
            {
                UpdateTextStyle = new UpdateTextStyleRequest
                {
                    TextStyle = new TextStyle
                    {
                        Bold = true,
                        FontSize = new Dimension { Magnitude = 12.0, Unit = "PT" },
                        WeightedFontFamily = new WeightedFontFamily { FontFamily = "Cambria" },
                        Link = new Link { Url = deliverableUrl }
                    },
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = urlStartIndex,
                        EndIndex = urlEndIndex
                    },
                    Fields = "link,bold,fontSize,weightedFontFamily"
                }
            });
            currentIndex += deliverableUrlText.Length;

            var horizontalRule = "_________________________________________________________________________\n\n";
            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = horizontalRule,
                    Location = new Location { Index = currentIndex }
                }
            });
            currentIndex += horizontalRule.Length;
        }

        var batchRequest = new BatchUpdateDocumentRequest { Requests = requests };
        var status = await _docsService.Documents.BatchUpdate(batchRequest, fileId).ExecuteAsync();
        var namedRangeIds = status.Replies
            .Where(reply => reply.CreateNamedRange != null)
            .Select(reply => reply.CreateNamedRange!.NamedRangeId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });

        for (var i = 0; i < namedRangeIds.Count; i++)
        {
            var bookmarkUrl = "https://docs.google.com/document/d/" + fileId + "/edit#bookmark=" + namedRangeIds[i];
            bookmarkInfos.Add(new PostBookmarkInfo
            {
                PostId = posts[i].DeliverableId,
                BookmarkUrl = bookmarkUrl
            });
        }

        return bookmarkInfos;
    }

    private static TextSegment? ParseMarkdownBold(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var boldRanges = new List<(int, int)>();
        var output = new StringBuilder();
        var index = 0;
        var regex = new Regex("\\*\\*(.*?)\\*\\*");
        foreach (Match match in regex.Matches(text))
        {
            output.Append(text.Substring(index, match.Index - index));
            var start = output.Length;
            output.Append(match.Groups[1].Value);
            var end = output.Length;
            boldRanges.Add((start, end));
            index = match.Index + match.Length;
        }

        output.Append(text.Substring(index));
        return new TextSegment
        {
            CleanText = output.ToString(),
            BoldRanges = boldRanges
        };
    }

    public async Task<List<Permission>> ShareDocumentWithUsersAsync(string documentId, List<string> emailAddresses)
    {
        var permissionResponses = new List<Permission>();
        foreach (var email in emailAddresses)
        {
            var permission = new Permission
            {
                Type = "user",
                Role = "writer",
                EmailAddress = email
            };
            var request = _driveService.Permissions.Create(permission, documentId);
            request.Fields = "id";
            try
            {
                permissionResponses.Add(await request.ExecuteAsync());
                Console.WriteLine("Shared document '" + documentId + "' with: " + email);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to share document with " + email + ": " + ex.Message);
            }
        }

        return permissionResponses;
    }

    public async Task<List<GoogleSheetRow>> ReadConfiguredSheetAsync()
    {
        var sheetUrl = _configuration["credit_menu"];
        if (string.IsNullOrWhiteSpace(sheetUrl))
        {
            sheetUrl = _configuration["GoogleSheet:Url"];
        }

        if (string.IsNullOrWhiteSpace(sheetUrl))
        {
            throw new InvalidOperationException("credit_menu is not configured in appsettings.json.");
        }

        var spreadsheetId = ExtractSpreadsheetId(sheetUrl);
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            throw new InvalidOperationException(
                "GoogleSheet:Url must be a Google Sheets link, such as https://docs.google.com/spreadsheets/d/{id}/edit.");
        }

        var exportUrl = "https://docs.google.com/spreadsheets/d/" + spreadsheetId + "/export?format=csv";
        var gid = ExtractSheetGid(sheetUrl);
        if (gid.HasValue)
        {
            exportUrl += "&gid=" + gid.Value.ToString();
        }

        using var drive = CreateReadOnlyDriveService();
        using var response = await drive.HttpClient.GetAsync(exportUrl);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || LooksLikeHtml(body))
        {
            throw new InvalidOperationException(
                "Unable to read the Google Sheet. Share the sheet with the API service account and confirm GoogleSheet:Url is a Google Sheets link.");
        }

        var table = ParseCsv(body);
        if (table.Count == 0)
        {
            return new List<GoogleSheetRow>();
        }

        var headerIndex = IndexHeaders(table[0]);
        string[] requiredColumns = ["Project", "Main Content Type", "Description", "Credits"];
        var missing = requiredColumns.Where(name => !headerIndex.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException("The sheet is missing columns: " + string.Join(", ", missing) + ".");
        }

        var rows = new List<GoogleSheetRow>();
        for (var i = 1; i < table.Count; i++)
        {
            var source = table[i];
            var row = new GoogleSheetRow
            {
                Project = CellByHeader(source, headerIndex, "Project"),
                MainContentType = CellByHeader(source, headerIndex, "Main Content Type"),
                Description = CellByHeader(source, headerIndex, "Description"),
                Credits = CellByHeader(source, headerIndex, "Credits"),
                TopicCategory = CellByHeader(source, headerIndex, "Topic Category")
            };

            if (string.IsNullOrWhiteSpace(row.Project) &&
                string.IsNullOrWhiteSpace(row.MainContentType) &&
                string.IsNullOrWhiteSpace(row.Description) &&
                string.IsNullOrWhiteSpace(row.Credits) &&
                string.IsNullOrWhiteSpace(row.TopicCategory))
            {
                continue;
            }

            rows.Add(row);
        }

        return rows;
    }

    public static string? ExtractSpreadsheetId(string sheetUrlOrId)
    {
        if (string.IsNullOrWhiteSpace(sheetUrlOrId))
        {
            return null;
        }

        var value = sheetUrlOrId.Trim();
        var spreadsheetMatch = Regex.Match(value, @"/spreadsheets/d/([a-zA-Z0-9_-]+)");
        if (spreadsheetMatch.Success)
        {
            return spreadsheetMatch.Groups[1].Value;
        }

        if (!value.Contains('/') && Regex.IsMatch(value, @"^[a-zA-Z0-9_-]+$"))
        {
            return value;
        }

        return null;
    }

    private static int? ExtractSheetGid(string sheetUrl)
    {
        var match = Regex.Match(sheetUrl, @"[#&?]gid=(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var gid))
        {
            return null;
        }

        return gid;
    }

    private static Dictionary<string, int> IndexHeaders(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = Regex.Replace(headers[i].Trim().TrimStart('\uFEFF'), @"\s+", " ");
            if (key.Length > 0 && !map.ContainsKey(key))
            {
                map[key] = i;
            }
        }

        return map;
    }

    private static string CellByHeader(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var index))
        {
            return string.Empty;
        }

        return Cell(row, index);
    }

    private static string Cell(IReadOnlyList<string> row, int index)
    {
        if (index < 0 || index >= row.Count)
        {
            return string.Empty;
        }

        return row[index].Trim();
    }

    private static bool LooksLikeHtml(string body)
    {
        var trimmed = body.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith("<", StringComparison.Ordinal);
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var current = csv[i];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (current == '"')
            {
                inQuotes = true;
            }
            else if (current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (current == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else if (current != '\r')
            {
                field.Append(current);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        if (rows.Count > 0 && rows[^1].All(string.IsNullOrEmpty))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }
}
