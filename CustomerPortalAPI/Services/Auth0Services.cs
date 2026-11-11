using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CustomerPortalAPI.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CustomerPortalAPI.Services
{
	public class Auth0Services
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public Auth0Services(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        // Client Credentials
        public async Task<string> GetAccessTokenAsync()
        {
            using var client = new HttpClient();
            var domain = _configuration["Auth0:Domain"];

            try
            {
                var url = "https://tech-brandingarc.us.auth0.com/oauth/token";

                // Creating the JSON payload
                var payload = new
                {
                    client_id = _configuration["Auth0:ClientId"],
                    client_secret = _configuration["Auth0:ClientSecret"],
                    audience = $"https://{domain}/api/v2/",
                    grant_type = "client_credentials"
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Sending the POST request
                HttpResponseMessage response = await client.PostAsync(url, content);

                // Checking the response
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);
                    Console.WriteLine($"Access Token: {jsonResponse.access_token}");
                    return jsonResponse.access_token;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
                    return null;
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

        public async Task<string> DeleteUserAsync(string id, string connection = "Username-Password-Authentication")
        {
            var token = await GetAccessTokenAsync();
            var domain = _configuration["Auth0:Domain"];

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.DeleteAsync($"https://{domain}/api/v2/users/:"+ id);

            if (response.IsSuccessStatusCode) {
                return "201";
            } else {
                var errorContent = await response.Content.ReadAsStringAsync();
                return errorContent;
            }
        }

        public async Task<string> AuthenticateUserAsync(string email, string password)
        {
            var domain = _configuration["Auth0:Domain"];

            // Define the Auth0 URL and your Auth0 configuration
            var authUrl = $"https://{domain}/oauth/token";
            var clientId = _configuration["Auth0:ClientId"];
            var clientSecret = _configuration["Auth0:ClientSecret"];
            
            // Prepare the JSON payload
            var payload = new
            {
                grant_type = "password",
                username = email,
                password = password,
                client_id = clientId,
                client_secret = clientSecret,
                scope = "openid profile email",
                connection = "Username-Password-Authentication"
            };

            try
            {
                using (var client = new HttpClient())
                {
                    // Serialize the payload to JSON
                    var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    // Send the POST request
                    var response = await client.PostAsync(authUrl, content);

                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // Read and return the JSON response
                        var responseBody = await response.Content.ReadAsStringAsync();
                        return responseBody;
                    }
                    else
                    {
                        // Handle errors
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Authentication failed: {error}");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network-related errors
                Console.WriteLine($"Network error: {httpEx.Message}");
                return $"Network error: {httpEx.Message}";
            }
            catch (Exception ex)
            {
                // Handle other types of errors
                Console.WriteLine($"An error occurred: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<bool> checkEmailInAuth0(string email)
        {
            var domain = _configuration["Auth0:Domain"];
            var token = await GetAccessTokenAsync();

            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "https://tech-brandingarc.us.auth0.com/api/v2/users-by-email?email="+email);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Authorization", "Bearer "+token);
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                JsonDocument jsonDocument = JsonDocument.Parse(result);
                JsonElement root = jsonDocument.RootElement;
                // Check if root is an array and if it is empty
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 0)
                {
                    Console.WriteLine("The root element is an empty array.");
                    return false;
                }
                else
                {
                    Console.WriteLine("The root element is not an empty array.");
                    return true;
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network-related errors
                Console.WriteLine($"Network error: {httpEx.Message}");
                //return $"Network error: {httpEx.Message}";
                return false;
            }
            catch (Exception ex)
            {
                // Handle other types of errors
                Console.WriteLine($"An error occurred: {ex.Message}");
                //return $"Error: {ex.Message}";
                return false;
            }
        }

        public async Task<string> createUserInAuth0(string email, string password, JObject jsonObj)
        {
            var domain = _configuration["Auth0:Domain"];
            var token = await GetAccessTokenAsync();

            try
            {
                var account = jsonObj["data"][0]["Account_Name"];
                string companyname = account["name"].ToString();
                string companyId = account["id"].ToString();
                string full_name = jsonObj["data"][0]["Full_Name"].ToString();
                string first_name = jsonObj["data"][0]["First_Name"].ToString();

                var data = new
                {
                    email = email,
                    user_metadata = new { company = companyname, companyId = companyId },
                    given_name = "string",
                    name = full_name,
                    connection = "Username-Password-Authentication",
                    email_verified = false,
                    nickname = first_name,
                    password = password,
                    verify_email = false
                };


                var jsonString = System.Text.Json.JsonSerializer.Serialize(data);
                var createUserRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{domain}/api/v2/users");
                createUserRequest.Headers.Add("Accept", "application/json");
                createUserRequest.Headers.Add("Authorization", "Bearer " + token);

                // Create StringContent with the serialized JSON
                var content = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
                createUserRequest.Content = content;

                using (var client = new HttpClient())
                {
                    var response = await client.SendAsync(createUserRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        // Handle success
                        var responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("User created successfully: " + responseData);
                        return responseData;
                    }
                    else
                    {
                        Console.WriteLine("Error creating user: " + response.StatusCode);
                        var res = await response.Content.ReadAsStringAsync();
                        return res;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network-related errors
                Console.WriteLine($"Network error: {httpEx.Message}");
                return $"Network error: {httpEx.Message}";
            }
            catch (Exception ex)
            {
                // Handle other types of errors
                Console.WriteLine($"An error occurred: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<string> createUserInAuth0Admin(User user)
        {
            var domain = _configuration["Auth0:Domain"];
            var token = await GetAccessTokenAsync();

            try
            {
                var data = new
                {
                    email = user.Email,
                    user_metadata = new { company = user.CompanyName, companyId = user.CompanyId },
                    given_name = "string",
                    name = user.full_name,
                    connection = "Username-Password-Authentication",
                    email_verified = false,
                    nickname = user.first_name,
                    password = user.Password,
                    verify_email = false
                };


                var jsonString = System.Text.Json.JsonSerializer.Serialize(data);
                var createUserRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{domain}/api/v2/users");
                createUserRequest.Headers.Add("Accept", "application/json");
                createUserRequest.Headers.Add("Authorization", "Bearer " + token);

                // Create StringContent with the serialized JSON
                var content = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
                createUserRequest.Content = content;

                using (var client = new HttpClient())
                {
                    var response = await client.SendAsync(createUserRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        // Handle success
                        var responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("User created successfully: " + responseData);
                        return responseData;
                    }
                    else
                    {
                        Console.WriteLine("Error creating user: " + response.StatusCode);
                        var res = await response.Content.ReadAsStringAsync();
                        return res;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network-related errors
                Console.WriteLine($"Network error: {httpEx.Message}");
                return $"Network error: {httpEx.Message}";
            }
            catch (Exception ex)
            {
                // Handle other types of errors
                Console.WriteLine($"An error occurred: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<string> assignRoleTOUserInAuth0Admin(string user_id)
        {
            var domain = _configuration["Auth0:Domain"];
            var token = await GetAccessTokenAsync();

            try
            {
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://tech-brandingarc.us.auth0.com/api/v2/roles");
                    request.Headers.Add("Accept", "application/json");
                    request.Headers.Add("Authorization", "Bearer " + token);
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    var createUserRequest = new HttpRequestMessage(HttpMethod.Post, $"https://tech-brandingarc.us.auth0.com/api/v2/users/{user_id}/roles");
                    createUserRequest.Headers.Add("Accept", "application/json");
                    createUserRequest.Headers.Add("Authorization", "Bearer " + token);

                    // Create StringContent with the serialized JSON
                    var jsonContent = $"{{\"roles\": [\"User\"]}}";
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    createUserRequest.Content = content;

                    response = await client.SendAsync(createUserRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        // Handle success
                        var responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("User created successfully: " + responseData);
                        return responseData;
                    }
                    else
                    {
                        Console.WriteLine("Error creating user: " + response.StatusCode);
                        var res = await response.Content.ReadAsStringAsync();
                        return res;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network-related errors
                Console.WriteLine($"Network error: {httpEx.Message}");
                return $"Network error: {httpEx.Message}";
            }
            catch (Exception ex)
            {
                // Handle other types of errors
                Console.WriteLine($"An error occurred: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<LoginResponse> Auth0UserLogin(string email, string password)
        {
            var auth0Domain = _configuration["Auth0:Domain"];
            var clientId = _configuration["Auth0:ClientId"];
            var clientSecret = _configuration["Auth0:ClientSecret"];

            var url = $"https://{auth0Domain}/oauth/token";

            var loginData = new
            {
                grant_type = "password",
                username = email,
                password = password,
                client_id = clientId,
                client_secret = clientSecret,
                scope = "openid profile email",
                audience = $"https://{auth0Domain}/api/v2/",
            };

            using var client = new HttpClient();
            var json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Error response content: " + errorContent);
                    return null; // Or handle the error as needed
                }
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                var data = JsonSerializer.Deserialize<LoginResponse>(responseBody);
                if (data == null)
                {
                    Console.WriteLine("Failed to deserialize response");
                    return null;
                }
                Console.WriteLine(data);
                var authUserInfo = await GetUserIdAsync(data.access_token);
                data.Role = authUserInfo.Roles;
                data.UserId = authUserInfo.UserId;
                return data;
            }
            catch (HttpRequestException e)
            {
                // Handle error (e.g., log, rethrow, or return null)
                Console.WriteLine($"Request error: {e.Message}");
                return null;
            }
        }

        private async Task<Auth0User> GetUserIdAsync(string accessToken)
        {
            var auth0Domain = _configuration["Auth0:Domain"];
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"https://{auth0Domain}/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve user profile.");
            }
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            Auth0User user = JsonSerializer.Deserialize<Auth0User>(responseBody);
            return user;
        }

        public async Task<string> UpdatePasswordAsync(string userId, string newPassword)
        {
            var domain = _configuration["Auth0:Domain"];
            var token = await GetAccessTokenAsync();

            var payload = new
            {
                password = newPassword,
                connection = "Username-Password-Authentication"
            };

            var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"https://{domain}/api/v2/users/{userId}"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            // Handle success
            var responseData = await response.Content.ReadAsStringAsync();
            Console.WriteLine("User created successfully: " + responseData);
            return responseData;
        }
    }
}

