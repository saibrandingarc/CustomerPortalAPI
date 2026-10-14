using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CustomerPortalAPI.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Intuit.Ipp.Data;
using Account = CustomerPortalAPI.Models.Account;
using Microsoft.Data.SqlClient;

namespace CustomerPortalAPI.Services
{
	public class ZohoServices
    {
        private const string DefaultContentFileGoogleUrl =
            "https://drive.google.com/drive/folders/1TiHjrFhsimDHb5VphpuNkxaCyIenAeRh?usp=drive_link";

        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public ZohoServices(ILoggerFactory loggerFactory, IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _logger = loggerFactory.CreateLogger<ZohoServices>();
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        public string GetAuthorizationUrl()
        {
            var clientId = _configuration["Zoho:ClientId"];
            var redirectUri = _configuration["Zoho:RedirectUri"];
            var authEndpoint = _configuration["Zoho:AuthEndpoint"];

            return $"{authEndpoint}?scope=AaaServer.profile.Read,ZohoCRM.modules.ALL&client_id={clientId}&response_type=token&access_type=offline&redirect_uri={redirectUri}";
        }

        // Method to Get Access Token
        public async Task<string> GetAccessTokenAsync()
        {
            var connectionString = _configuration.GetConnectionString("ZohoConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ZohoConnection is not configured.");

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Get latest settings
            var selectQuery = @"
                SELECT TOP 1 Id, AccessToken, RefreshToken, ExpiresIn, DateAdded, ClientId, ClientSecret, TokenEndpoint, Code
                FROM Settings
                WHERE tokenFrom = 'zoho'
                ORDER BY DateAdded DESC";

            int id = 0;
            string accessToken = "";
            string refreshToken = "";
            int expiresIn = 0;
            DateTime dateAdded = DateTime.UtcNow;
            var clientId = "";
            var clientSecret = "";
            var tokenEndpoint = "";
            var code = "";
            using (var cmd = new SqlCommand(selectQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    id = reader.GetInt32(0);
                    accessToken = reader.GetString(1);
                    refreshToken = reader.GetString(2);
                    expiresIn = reader.GetInt32(3);
                    dateAdded = reader.GetDateTime(4);
                    clientId = reader.GetString(5);
                    clientSecret = reader.GetString(6);
                    tokenEndpoint = reader.GetString(7);
                    code = reader.GetString(8);
                }
            }

            var client = new HttpClient();
            var parameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "authorization_code" }
            };
            try
            {
                var response = await client.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (responseData.ContainsKey("error")) {
                        Console.WriteLine("Error: Missing required key");
                        return "";
                    } else {
                        Console.WriteLine("no errors");
                        accessToken = await AddSettingsAsync(responseData);
                        return accessToken;
                    }
                }
                else
                {
                    throw new Exception("Failed to regenerate token.");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return ex.Message;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            var connectionString = _configuration.GetConnectionString("ZohoConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ZohoConnection is not configured.");

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Get latest settings
            var selectQuery = @"
                SELECT TOP 1 Id, AccessToken, RefreshToken, ExpiresIn, DateAdded, ClientId, ClientSecret, TokenEndpoint, Code
                FROM Settings
                WHERE tokenFrom = 'zoho'
                ORDER BY DateAdded DESC";

            int id = 0;
            string accessToken = "";
            string refreshToken = "";
            int expiresIn = 0;
            DateTime dateAdded = DateTime.UtcNow;
            var clientId = "";
            var clientSecret = "";
            var tokenEndpoint = "";
            var code = "";
            using (var cmd = new SqlCommand(selectQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    id = reader.GetInt32(0);
                    accessToken = reader.GetString(1);
                    refreshToken = reader.GetString(2);
                    expiresIn = reader.GetInt32(3);
                    dateAdded = reader.GetDateTime(4);
                    clientId = reader.GetString(5);
                    clientSecret = reader.GetString(6);
                    tokenEndpoint = reader.GetString(7);
                    code = reader.GetString(8);
                }
            }

            // 2. If no data → generate new token
            if (id == 0)
            {
                var tokenObj = await GetAccessTokenAsync();

                if (string.IsNullOrWhiteSpace(tokenObj))
                    return "Invalid Token";

                return tokenObj;
            }

            // 3. Check expiration
            var expirationTime = dateAdded.AddSeconds(expiresIn);

            if (DateTime.UtcNow >= expirationTime)
            {
                // Token expired → regenerate
                var newToken = await RegenerateTokenAsync(refreshToken, clientId, clientSecret, tokenEndpoint, code);

                if (string.IsNullOrWhiteSpace(newToken))
                {
                    _logger.LogError("Zoho token refresh returned an empty token.");
                    return "Invalid Token";
                }

                // 4. Update DB
                var updateQuery = @"
                    UPDATE Settings
                    SET AccessToken = @AccessToken,
                        DateAdded = @DateAdded
                    WHERE Id = @Id";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@AccessToken", newToken);
                updateCmd.Parameters.AddWithValue("@DateAdded", DateTime.UtcNow);
                updateCmd.Parameters.AddWithValue("@Id", id);

                await updateCmd.ExecuteNonQueryAsync();

                return newToken;
            } else
            {
                if (string.IsNullOrWhiteSpace(accessToken)
                    || accessToken == "Invalid Token"
                    || !accessToken.StartsWith("1000.", StringComparison.Ordinal))
                {
                    var tokenObj = await GetAccessTokenAsync();
                    Console.WriteLine("token :" + tokenObj);
                }
            }

            // 5. Token still valid
            return accessToken;
        }

        private async Task<string> RegenerateTokenAsync(string refreshToken, string clientId, string clientSecret, string tokenEndpoint, string code)
        {
            // Replace with actual API call to Zoho to regenerate the token
            using (var httpClient = new HttpClient())
            {
                var parameters = new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "refresh_token" }
                };

                try
                {
                    var response = await httpClient.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                        if (responseData == null)
                        {
                            _logger.LogError("Zoho refresh response could not be parsed. Body: {Body}", json);
                            return "";
                        }

                        if (responseData.TryGetValue("access_token", out var accessToken) && !string.IsNullOrWhiteSpace(accessToken))
                        {
                            return accessToken;
                        }

                        var errorValue = responseData.TryGetValue("error", out var error) ? error : "unknown_error";
                        _logger.LogError("Zoho refresh token failed. Error: {Error}. Body: {Body}", errorValue, json);
                        return "";
                    }
                    else
                    {
                        throw new Exception("Failed to regenerate token.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"Request failed: {ex}");
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Request failed: {ex}");
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string?> AddSettingsAsync(Dictionary<string, string>? responseData)
        {
            if (responseData == null ||
                !responseData.TryGetValue("access_token", out var accessToken) ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Cannot persist Zoho settings because access_token is missing.");
                return null;
            }
            var connectionString = _configuration.GetConnectionString("ZohoConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // 1. Check existing record
                var selectQuery = @"
            SELECT TOP 1 Id 
            FROM Settings 
            WHERE tokenFrom = 'zoho' 
            ORDER BY DateAdded DESC";

                int? existingId = null;

                using (var selectCmd = new SqlCommand(selectQuery, connection))
                using (var reader = await selectCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        existingId = reader.GetInt32(0);
                    }
                }

                if (existingId != null)
                {
                    // 2. Update existing
                    var updateQuery = @"
                UPDATE Settings
                SET AccessToken = @AccessToken,
                    DateAdded = @DateAdded
                WHERE Id = @Id";

                    using (var updateCmd = new SqlCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@AccessToken", accessToken);
                        updateCmd.Parameters.AddWithValue("@DateAdded", DateTime.UtcNow);
                        updateCmd.Parameters.AddWithValue("@Id", existingId);

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Validate refresh token
                    if (!responseData.TryGetValue("refresh_token", out var refreshToken) ||
                        string.IsNullOrWhiteSpace(refreshToken))
                    {
                        _logger.LogError("Cannot create Zoho settings because refresh_token is missing.");
                        return null;
                    }

                    var scope = responseData.TryGetValue("scope", out var scopeValue) ? scopeValue : string.Empty;
                    var expiresIn = responseData.TryGetValue("expires_in", out var expiresInRaw) &&
                                    int.TryParse(expiresInRaw, out var expiresInValue)
                                        ? expiresInValue
                                        : 3600;

                    // 3. Insert new
                    var insertQuery = @"
                INSERT INTO Settings (AccessToken, RefreshToken, Scope, ExpiresIn, DateAdded, tokenFrom)
                VALUES (@AccessToken, @RefreshToken, @Scope, @ExpiresIn, @DateAdded, 'zoho')";

                    using (var insertCmd = new SqlCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@AccessToken", accessToken);
                        insertCmd.Parameters.AddWithValue("@RefreshToken", refreshToken);
                        insertCmd.Parameters.AddWithValue("@Scope", scope);
                        insertCmd.Parameters.AddWithValue("@ExpiresIn", expiresIn);
                        insertCmd.Parameters.AddWithValue("@DateAdded", DateTime.UtcNow);

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return accessToken;
        }

        public async Task<string> GetZohoContactByEmailAsync(string email)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email=" + email)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var contact = await response.Content.ReadAsStringAsync();
                    return contact;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCasesAsync()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases?fields=Case_Number,Subject,Status,Account_Name")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            //return "test : "+accessToken;
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Accounts/" + companyId)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<(byte[] Content, string ContentType)?> GetZohoAccountPhotoAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            if (accessToken == "Invalid Token" || string.IsNullOrWhiteSpace(companyId))
            {
                return null;
            }

            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://www.zohoapis.com/crm/v7/Accounts/" + companyId + "/photo")
                {
                    Headers = { { "Authorization", $"Bearer {accessToken}" } }
                };

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent
                    || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var content = await response.Content.ReadAsByteArrayAsync();
                if (content.Length == 0)
                {
                    return null;
                }

                return (content, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetZohoCasesByCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    //var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=(Account_Name.id:equals:" + companyId + ") and (Status:equals:New)")
                    //{
                    //    Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    //};

                    //var response = await _httpClient.SendAsync(request);
                    //Console.WriteLine($"Request failed: {response}");
                    //response.EnsureSuccessStatusCode();

                    //var cases = await response.Content.ReadAsStringAsync();
                    //return cases;

                    var url = "https://www.zohoapis.com/crm/v7/coql";

                    var query = new
                    {
                        select_query = $"select id, Case_Number, Subject, Status, Case_Open_Date, Case_Closed_Date, Type, Case_Reason, Reported_By, Email, Created_Time, Account_Name from Cases where (Account_Name.id = {companyId} )"
                    };

                    var jsonContent = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(query),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = jsonContent
                    };

                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await _httpClient.SendAsync(request);

                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoWebsiteByCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token" || string.IsNullOrWhiteSpace(companyId))
            {
                return null;
            }

            try
            {
                var byId = await GetZohoWebsitePayloadAsync(
                    accessToken,
                    "https://www.zohoapis.com/crm/v7/Websites/" + companyId);
                if (byId != null)
                {
                    return byId;
                }

                var criteria = "(Account_Name.id:equals:" + companyId + ")";
                var searchUrl = "https://www.zohoapis.com/crm/v7/Websites/search?criteria="
                    + Uri.EscapeDataString(criteria);
                return await GetZohoWebsitePayloadAsync(accessToken, searchUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return ex.Message;
            }
        }

        private async Task<string?> GetZohoWebsitePayloadAsync(string accessToken, string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return body.TrimStart().StartsWith("{") ? body : null;
        }

        public async Task<string> GetZohoCasesByCompanyForDahsboardAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    // Calculate the date 2 years ago
                    DateTime twoYearsAgo = DateTime.UtcNow.AddYears(-2);
                    string formattedDate = twoYearsAgo.ToString("yyyy-MM-dd");

                    // Build the criteria
                    string criteria = $"(Case_Open_Date:greater_than:{formattedDate})";
                    string criteria2 = "(Account_Name.id:equals: " + companyId + ")";
                    string encodedCriteria = Uri.EscapeDataString(criteria);

                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=("+ criteria + " and " + criteria2 + ")")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    //response.EnsureSuccessStatusCode();
                    if (response.IsSuccessStatusCode)
                    {
                        var cases = await response.Content.ReadAsStringAsync();
                        return cases;
                    }

                    // Handle failure
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return errorContent;
                    
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCaseByIdAsync(string companyId, string caseNumber)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/" + caseNumber)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<(int StatusCode, string Body)?> GetZohoCaseTimelineAsync(string caseId)
        {
            if (string.IsNullOrWhiteSpace(caseId) || !caseId.All(char.IsDigit))
            {
                return (400, "{\"message\":\"Case id must be a Zoho record id.\"}");
            }

            var accessToken = await GetValidTokenAsync();
            if (accessToken == "Invalid Token" || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            var combined = new JArray();
            string? pageToken = null;
            var moreRecords = true;

            for (var page = 0; page < 5 && moreRecords; page++)
            {
                var url =
                    "https://www.zohoapis.com/crm/v8/Cases/" + caseId +
                    "/__timeline?sort_by=audited_time&sort_order=desc" +
                    "&include_inner_details=field_history.field_label,field_history.data_type";
                if (!string.IsNullOrEmpty(pageToken))
                {
                    url += "&page_token=" + Uri.EscapeDataString(pageToken);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Headers = { { "Authorization", $"Bearer {accessToken}" } }
                };

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    break;
                }

                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return ((int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? "{}" : body);
                }

                var payload = JObject.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                if (payload["__timeline"] is JArray entries)
                {
                    foreach (var entry in entries)
                    {
                        combined.Add(entry);
                    }
                }

                var info = payload["info"] as JObject;
                pageToken = info?["next_page_token"]?.ToString();
                moreRecords = info?["more_records"]?.Value<bool>() == true && !string.IsNullOrEmpty(pageToken);
            }

            var result = new JObject
            {
                ["__timeline"] = combined,
                ["info"] = new JObject
                {
                    ["count"] = combined.Count,
                    ["more_records"] = false
                }
            };
            return (200, result.ToString(Formatting.None));
        }

        public async Task<string> GetZohoDeliverablesByIdAsync(string deliverableId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Deliverables_V2/" + deliverableId)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var deliverables = await response.Content.ReadAsStringAsync();
                    return AddContentFileGoogleUrlToDeliverables(deliverables);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoDeliverablesByCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria=((Account.id:equals:{companyId})AND((Status:not_equal:Completed)AND(Status:not_equal:'Cancelled')AND(Status:not_equal:'Approval Idea')))"
                    )
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    //var statuses = new[] { "completed", "cancelled", "idea" };

                    //var statusCriteria = string.Join(" OR ", statuses.Select(s => $"(Status:equals:{s})"));

                    //var criteria = $"((Account.id:equals:{companyId})AND({statusCriteria}))";

                    //var request = new HttpRequestMessage(
                    //    HttpMethod.Get,
                    //    $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria={criteria}"
                    //)
                    //{
                    //    Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    //};

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var deliverables = await response.Content.ReadAsStringAsync();
                    return AddContentFileGoogleUrlToDeliverables(deliverables);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<(int StatusCode, string Body)?> GetZohoDeliverablesByAccountAndYearAsync(string companyId, int year)
        {
            var accessToken = await GetValidTokenAsync();
            if (accessToken == "Invalid Token" || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            var criteria = $"((Account.id:equals:{companyId})AND(Block:starts_with:{year}))";
            var records = new JArray();
            var moreRecords = false;

            for (var page = 1; page <= 10; page++)
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria={criteria}&per_page=200&page={page}"
                )
                {
                    Headers = { { "Authorization", $"Bearer {accessToken}" } }
                };

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    break;
                }

                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Zoho deliverables search failed for account {AccountId} year {Year} with status {Status}.",
                        companyId,
                        year,
                        (int)response.StatusCode);
                    return ((int)response.StatusCode, string.IsNullOrWhiteSpace(body)
                        ? JsonConvert.SerializeObject(new { message = "Zoho CRM request failed." })
                        : body);
                }

                var json = JObject.Parse(body);
                if (json["data"] is JArray pageData)
                {
                    foreach (var item in pageData)
                    {
                        records.Add(item);
                    }
                }

                moreRecords = json["info"]?["more_records"]?.Value<bool>() == true;
                if (!moreRecords)
                {
                    break;
                }
            }

            var sorted = new JArray(
                records
                    .OrderBy(record => record["Block"]?.ToString(), StringComparer.Ordinal)
                    .ThenBy(record => record["Name"]?.ToString(), StringComparer.Ordinal));

            var result = new JObject
            {
                ["data"] = sorted,
                ["info"] = new JObject
                {
                    ["count"] = sorted.Count,
                    ["more_records"] = moreRecords,
                    ["year"] = year,
                    ["account_id"] = companyId
                }
            };

            return (StatusCodes.Status200OK, AddContentFileGoogleUrlToDeliverables(result.ToString(Formatting.None)));
        }

        public async Task<string> GetZohoDeliverablesByCompanyAndBlockAsync(string companyId, string blockId, string status)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria=((Account.id:equals:{companyId})AND(Block:starts_with:{blockId})AND(Status:equals:{status}))"
                    );
                    if (status == "Completed")
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria=((Account.id:equals:{companyId})AND(Block:starts_with:{blockId})AND(Status:equals:{status}))")
                        {
                            Headers = { { "Authorization", $"Bearer {accessToken}" } }
                        };
                    } else
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, $"https://www.zohoapis.com/crm/v7/Deliverables_V2/search?criteria=((Account.id:equals:{companyId})AND(Status:starts_with:{status}))")
                        {
                            Headers = { { "Authorization", $"Bearer {accessToken}" } }
                        };
                    }
                    string criteria = $"(Account.id:equals:{companyId} and Block:equals:{blockId})";
                    

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    //request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    response.EnsureSuccessStatusCode();
                    var deliverables = await response.Content.ReadAsStringAsync();
                    return AddContentFileGoogleUrlToDeliverables(deliverables);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.ToString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.ToString();
                }
            }
        }

        private static string AddContentFileGoogleUrlToDeliverables(
            string deliverablesJson,
            string googleFolderUrl = DefaultContentFileGoogleUrl)
        {
            if (string.IsNullOrWhiteSpace(deliverablesJson))
            {
                return deliverablesJson;
            }

            try
            {
                var json = JObject.Parse(deliverablesJson);
                if (json["data"] is JArray dataArray)
                {
                    foreach (var deliverable in dataArray.OfType<JObject>())
                    {
                        deliverable["content_file_google_url"] = googleFolderUrl;
                    }
                }

                return json.ToString(Formatting.None);
            }
            catch (JsonReaderException)
            {
                return deliverablesJson;
            }
        }

        public async Task<string> SaveZohoCasesAsync(
            CaseModel caseObj,
            IEnumerable<IFormFile>? attachments = null)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var acc = new Account { id = caseObj.Account_Name.id };
                    var caseData = new
                    {
                        data = new[]
                        {
                            new CaseModel
                            {
                                //Owner = new Owner { id = "user-id" },
                                //Product_Name = new Owner { id = "product-id" },
                                //Deal_Name = new Owner { id = "deal-id" },
                                Account_Name = acc,
                                //Related_To = new Owner { id = "contact-id" },
                                Status = "New",
                                Email = caseObj.Email,
                                Description = caseObj.Description,
                                Internal_Comments = "",
                                Priority = "Medium",
                                Reported_By = string.IsNullOrWhiteSpace(caseObj.Reported_By)
                                    ? caseObj.Email
                                    : caseObj.Reported_By.Trim(),
                                Case_Origin = "",
                                Case_Reason = "",
                                Subject = caseObj.Subject,
                                Type = "",
                                Phone = "",
                                Email_Notes1 = caseObj.Email_Notes1
                            }
                        }
                    };

                    // Serialize the case data to JSON
                    string jsonPayload = System.Text.Json.JsonSerializer.Serialize(caseData);
                    Console.WriteLine(jsonPayload);
                    // Create StringContent with the JSON payload, UTF-8 encoding, and application/json MIME type
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    Console.WriteLine(content);

                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/Cases");
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");

                    //var content = new StringContent("{\n\t\"data\": [\n\t\t{\n            \"Account_Name\": {\n                \"id\": \"3293516000108143104\"\n            },\n            \"Status\": \"\",\n            \"Description\": \"test description\",\n            \"Internal_Comments\": \"\",\n            \"Priority\": \"\",\n            \"Reported_By\": \"\",\n            \"Case_Origin\": \"\",\n            \"Case_Reason\": \"\",\n            \"Subject\": \"test subject 1\",\n            \"Type\": \"\",\n            \"Phone\": \"\"\n        }\n\t]\n}", null, "application/json");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(response_json);

                    if (attachments != null)
                    {
                        var attachmentList = attachments.Where(a => a.Length > 0).ToList();
                        if (attachmentList.Count > 0)
                        {
                            var caseId = TryExtractCreatedCaseId(response_json);
                            if (string.IsNullOrWhiteSpace(caseId))
                            {
                                throw new InvalidOperationException(
                                    "Case was created but the record id could not be determined for attachment upload.");
                            }

                            foreach (var file in attachmentList)
                            {
                                await UploadCaseAttachmentAsync(accessToken, caseId, file);
                            }
                        }
                    }

                    return response_json;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        private static string? TryExtractCreatedCaseId(string zohoResponseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(zohoResponseJson);
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
                {
                    return null;
                }

                var first = data[0];
                if (!first.TryGetProperty("details", out var details) ||
                    !details.TryGetProperty("id", out var idElement))
                {
                    return null;
                }

                return idElement.GetString();
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private async System.Threading.Tasks.Task UploadCaseAttachmentAsync(string accessToken, string caseId, IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var multipartContent = new MultipartFormDataContent();
            var streamContent = new StreamContent(stream);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipartContent.Add(streamContent, "file", file.FileName);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://www.zohoapis.com/crm/v7/Cases/{caseId}/Attachments");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = multipartContent;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task<string> UpdateZohoCasesAsync(string jsonPayload, string id)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    // Create StringContent with the JSON payload, UTF-8 encoding, and application/json MIME type
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    Console.WriteLine(content);

                    var request = new HttpRequestMessage(HttpMethod.Put, "https://www.zohoapis.com/crm/v7/Cases/"+id);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");

                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return response_json.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> checkEmail(string email)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email="+email)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    var content = new StringContent("", null, "text/plain");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var userInfo = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return userInfo;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCompanies()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Accounts/search?criteria=(MD_Status:equals:Active)&fields=Account_Name,Short_Name")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    var content = new StringContent("", null, "text/plain");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var userInfo = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return userInfo;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> DeleteZohoCasesAsync(string id)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Delete, "https://www.zohoapis.com/crm/v7/Cases/"+ id);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return response_json.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> UpdateDeliverableCreativeNotesAsync(string deliverableId, string notesEntry)
        {
            var accessToken = await GetValidTokenAsync();
            if (accessToken == "Invalid Token")
            {
                return null;
            }

            try
            {
                var deliverable = await GetZohoDeliverablesByIdAsync(deliverableId);
                using JsonDocument doc = JsonDocument.Parse(deliverable);
                JsonElement root = doc.RootElement;
                var dataElement = root.GetProperty("data")[0];

                string creativeNotes = string.Empty;
                if (dataElement.TryGetProperty("Creative_Notes", out var creativeNotesElement)
                    && creativeNotesElement.ValueKind == JsonValueKind.String)
                {
                    creativeNotes = creativeNotesElement.GetString() ?? string.Empty;
                }

                string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string formattedMessage = $"{currentTime}: {notesEntry}";

                string updatedNotes = string.IsNullOrWhiteSpace(creativeNotes)
                    ? formattedMessage
                    : $"{creativeNotes}\n{formattedMessage}";

                var caseData = new
                {
                    data = new[]
                    {
                        new DeliverableModel
                        {
                            Creative_Notes = updatedNotes
                        }
                    }
                };

                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(caseData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Put, "https://www.zohoapis.com/crm/v7/Deliverables_V2/" + deliverableId);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return ex.Message;
            }
        }

        public async Task<string> UpdateDeliverableClientApprovalAsync(string deliverableId, string status, string message)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var deliverable = await GetZohoDeliverablesByIdAsync(deliverableId);
                    using JsonDocument doc = JsonDocument.Parse(deliverable);
                    JsonElement root = doc.RootElement;

                    // Navigate to data[0].Client_Notes
                    string clientNotes = root.GetProperty("data")[0].GetProperty("Client_Notes").GetString();

                    // Safely append date/time and retain existing notes
                    string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string formattedMessage = $"{currentTime}: {message}";

                    string updatedNotes = string.IsNullOrWhiteSpace(clientNotes)
                        ? formattedMessage
                        : $"{clientNotes}\n{formattedMessage}";

                    var caseData = new
                    {
                        data = new[]
                        {
                            new DeliverableModel
                            {
                                Client_Notes = updatedNotes,
                                Client_Approval_Status = status
                            }
                        }
                    };

                    // Serialize the case data to JSON
                    string jsonPayload = System.Text.Json.JsonSerializer.Serialize(caseData);
                    Console.WriteLine(jsonPayload);
                    // Create StringContent with the JSON payload, UTF-8 encoding, and application/json MIME type
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    Console.WriteLine(content);

                    var request = new HttpRequestMessage(HttpMethod.Put, "https://www.zohoapis.com/crm/v7/Deliverables_V2/"+deliverableId);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");

                    //var content = new StringContent("{\n\t\"data\": [\n\t\t{\n            \"Account_Name\": {\n                \"id\": \"3293516000108143104\"\n            },\n            \"Status\": \"\",\n            \"Description\": \"test description\",\n            \"Internal_Comments\": \"\",\n            \"Priority\": \"\",\n            \"Reported_By\": \"\",\n            \"Case_Origin\": \"\",\n            \"Case_Reason\": \"\",\n            \"Subject\": \"test subject 1\",\n            \"Type\": \"\",\n            \"Phone\": \"\"\n        }\n\t]\n}", null, "application/json");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return response_json.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }
    }
}

