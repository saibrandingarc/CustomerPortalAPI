using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Intuit.Ipp.Core.Configuration;
using Intuit.Ipp.Data;
using Intuit.Ipp.OAuth2PlatformClient;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;
using CustomerPortalAPI.Models;
using static System.Net.Mime.MediaTypeNames;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CustomerPortalAPI.Services
{
	public class QuickBookServices
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public QuickBookServices(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        // Client Credentials
        public async Task<string> ExchangeAuthorizationCode(string code)
        {
            using var client = new HttpClient();
            var ClientId = _configuration["intuit:ClientId"];
            var ClientSecret = _configuration["intuit:ClientSecret"];
            var redirectUrl = _configuration["intuit:redirectUrl"];
            //var scopes = _configuration["intuit:scopes"];
            var tokenUrl = _configuration["intuit:tokenUrl"];
            //var code = _configuration["intuit:code"];
            var environment = "sandbox";
            try
            {
                // Instantiate object
                OAuth2Client oauthClient = new(ClientId, ClientSecret, redirectUrl, environment); // environment is “sandbox” or “production”

                //Prepare scopes
                List<OidcScopes> scopes = new List<OidcScopes>();
                scopes.Add(OidcScopes.Accounting);

                //Get the authorization URL
                string authorizeUrl = oauthClient.GetAuthorizationURL(scopes);

                // Create Authorization header (Base64 encode ClientID:ClientSecret)
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                // Create POST request body
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUrl)
                });

                var response = await client.PostAsync(tokenUrl, content);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error exchanging authorization code: {response.ReasonPhrase}");

                var responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
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

        public async Task<string> getInvoices(string CustomerName)
        {
            var existData = await _context.Settings
                    .Where(p => p.tokenFrom == "quickbooks")
                    .OrderByDescending(p => p.DateAdded)
                    .FirstOrDefaultAsync();
            var accessToken = existData.AccessToken;
            bool isExpired = IsAccessTokenExpired(existData.DateAdded, existData.ExpiresIn);

            if (isExpired)
            {
                accessToken = await RefreshToken();
                Console.WriteLine("Access token is expired.");
            }

            var ApiBaseUrl = _configuration["intuit:baseURL"];
            var CompanyId = _configuration["intuit:realmid"];
            
            try
            {
                var cust = await getCustomerId(CompanyId, CustomerName);
                
                var document = JsonDocument.Parse(cust);

                // Navigate to the "Id" field
                var root = document.RootElement;
                var queryResponse = root.GetProperty("QueryResponse");
                
                if (queryResponse.TryGetProperty("Customer", out JsonElement customerElement))
                {
                    // Property exists
                    var customerArray = customerElement;
                    
                    //var customerArray = queryResponse.GetProperty("Customer");
                    if (customerArray.GetArrayLength() > 0)
                    {
                        var customer = customerArray[0];
                        string customerId = customer.GetProperty("Id").GetString();

                        //string apiBaseUrl = $"{ApiBaseUrl}{CompanyId}/query";
                        string query = "SELECT * FROM Invoice WHERE CustomerRef = '" + customerId + "'";
                        
                        string apiBaseUrl = $"https://quickbooks.api.intuit.com/v3/company/9341455985622308/query?include=invoiceLink&query={query}&minorversion=74";
                        //return "inside try after response : " + apiBaseUrl;
                        using (var client = new HttpClient())
                        {
                            // Set the Authorization header
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                            // Build the complete request URI with query parameters
                            var requestUri = $"{apiBaseUrl}?query={Uri.EscapeDataString(query)}";
                            
                            // Send the GET request
                            var response = await client.GetAsync(apiBaseUrl);
                            //return "inside try after response : " + response + " ---- "+accessToken;
                            // Check for success and return the response content
                            if (response.IsSuccessStatusCode)
                            {
                                var content = await response.Content.ReadAsStringAsync();
                                return content;
                            }
                            else
                            {

                                // Handle errors
                                var errorContent = await response.Content.ReadAsStringAsync();
                                return errorContent;
                            }
                        }
                    }
                    else
                    {
                        return "No customers found in the response.";
                    }
                }
                else
                {
                    // Property does not exist
                    return "No invoices for the customer";
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                return "http : "+ex.ToString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return "http direct : " + ex.ToString();
            }
        }

        public async Task<string> RefreshToken()
        {
            using (var client = new HttpClient())
            {
                var existData = await _context.Settings
                    .Where(p => p.tokenFrom == "quickbooks")
                    .OrderByDescending(p => p.DateAdded)
                    .FirstOrDefaultAsync();

                var refreshToken = existData.RefreshToken;
                var ClientId = _configuration["intuit:ClientId"];
                var ClientSecret = _configuration["intuit:ClientSecret"];
                var TokenUrl = _configuration["intuit:tokenUrl"];
                var AccessToken = existData.AccessToken;

                // Set up the request content
                var body = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("client_secret", ClientSecret)
                });

                
                //string jsonBody = JsonConvert.SerializeObject(body);
                //client.DefaultRequestHeaders.Accept.Clear();
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedBytes);
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // Send the POST request
                var response = await client.PostAsync(TokenUrl, body);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    var accessToken = await AddQuickbookSettingsAsync(jsonResponse);
                    // Extract the access token from the response
                    AccessToken = jsonResponse["access_token"];
                    Console.WriteLine(AccessToken);
                    return AccessToken;
                }

                // Handle failure
                var errorContent = await response.Content.ReadAsStringAsync();
                return errorContent;
            }
        }

        public async Task<string?> AddQuickbookSettingsAsync(Dictionary<string, string>? responseData)
        {
            var accessToken = responseData["access_token"];
            var existData = await _context.Settings
                .Where(p => p.tokenFrom == "quickbooks")
                .OrderByDescending(p => p.DateAdded)
                .FirstOrDefaultAsync();

            if (existData != null)
            {
                existData.DateAdded = DateTime.UtcNow;
                existData.AccessToken = accessToken;
                existData.RefreshToken = responseData["refresh_token"];

                // Save changes
                await _context.SaveChangesAsync();
            } else {
                var newSetting = new Settings
                {
                    AccessToken = responseData["access_token"],
                    RefreshToken = responseData["refresh_token"],
                    Scope = "com.intuit.quickbooks.accounting, com.intuit.quickbooks.payment, openid, profile, email, phone, address",
                    ExpiresIn = int.Parse(responseData["expires_in"]),
                    DateAdded = DateTime.UtcNow,
                    tokenFrom = "quickbooks"
                };

                _context.Settings.Update(newSetting);
            }

            await _context.SaveChangesAsync();
            return accessToken;
        }

        public bool IsAccessTokenExpired(DateTime createdAt, int expiresIn)
        {
            // Calculate the expiration time
            var expirationTime = createdAt.AddSeconds(expiresIn);

            // Compare current time with expiration time
            return DateTime.UtcNow > expirationTime;
        }

        public async Task<Stream> getInvoicePDF(string invoiceId)
        {
            var existData = await _context.Settings
                    .Where(p => p.tokenFrom == "quickbooks")
                    .OrderByDescending(p => p.DateAdded)
                    .FirstOrDefaultAsync();
            var accessToken = existData.AccessToken;
            bool isExpired = IsAccessTokenExpired(existData.DateAdded, existData.ExpiresIn);

            if (isExpired)
            {
                accessToken = await RefreshToken();
                Console.WriteLine("Access token is expired.");
            }

            var ApiBaseUrl = _configuration["intuit:baseURL"];
            var CompanyId = _configuration["intuit:realmid"];
            try
            {
                string apiBaseUrl = $"https://quickbooks.api.intuit.com/v3/company/{CompanyId}/invoice/{invoiceId}/pdf?minorversion=40";
                using (var client = new HttpClient())
                {
                    // Set the Authorization header
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));

                    // Send the GET request
                    var response = await client.GetAsync(apiBaseUrl);

                    // Check for success and return the response content
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStreamAsync();
                        //return File(pdfStream, "application/pdf", $"Invoice_{invoiceId}.pdf");
                    } else
                    {
                        throw new Exception("Failed to fetch PDF");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                throw new Exception($"Failed to fetch PDF: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw new Exception($"Failed to fetch PDF: {ex.Message}");
            }
        }

        public async Task<string> getCustomerId(string CompanyId, string customername)
        {
            var existData = await _context.Settings
                    .Where(p => p.tokenFrom == "quickbooks")
                    .OrderByDescending(p => p.DateAdded)
                    .FirstOrDefaultAsync();
            var accessToken = existData.AccessToken;
            bool isExpired = IsAccessTokenExpired(existData.DateAdded, existData.ExpiresIn);

            if (isExpired)
            {
                accessToken = await RefreshToken();
                Console.WriteLine("Access token is expired.");
            }

            var ApiBaseUrl = _configuration["intuit:baseURL"];
            try
            {
                //string apiBaseUrl = $"{ApiBaseUrl}{CompanyId}/query";
                string query = "SELECT * FROM Customer WHERE DisplayName = '"+ customername +"'";
                //string query = "select * from Customer Where Metadata.LastUpdatedTime > '2015-03-01'";
                string apiBaseUrl = $"https://quickbooks.api.intuit.com/v3/company/{CompanyId}/query?query={query}&minorversion=74";

                using (var client = new HttpClient())
                {
                    // Set the Authorization header
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Build the complete request URI with query parameters
                    var requestUri = $"{apiBaseUrl}?query={Uri.EscapeDataString(query)}";

                    // Send the GET request
                    var response = await client.GetAsync(apiBaseUrl);
                    // Check for success and return the response content
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return content;
                    }
                    else
                    {

                        // Handle errors
                        var errorContent = await response.Content.ReadAsStringAsync();
                        return "Error Message : "+errorContent;
                    }
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
    }
}

