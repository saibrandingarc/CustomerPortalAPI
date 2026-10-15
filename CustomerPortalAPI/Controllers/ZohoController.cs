using Microsoft.AspNetCore.Mvc;
using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using System.Text.Json;
using Intuit.Ipp.Core.Configuration;
using Intuit.Ipp.Data;
using User = CustomerPortalAPI.Models.User;

namespace CustomerPortalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class ZohoController : ControllerBase
{
    private readonly ZohoServices _zohoOAuthService;
    private readonly UserService _userService;
    private readonly EmailService _emailService;
    private readonly Auth0Services _auth0Service;

    private const int FailedLoginThreshold = 3;
    private const int FailedWindowMinutes = 15;
    private const int LockoutMinutes = 15;

    public ZohoController(ZohoServices zohoOAuthService, UserService userService, EmailService emailService, Auth0Services auth0Services)
    {
        _zohoOAuthService = zohoOAuthService;
        _userService = userService;
        _emailService = emailService;
        _auth0Service = auth0Services;
    }

    [HttpGet("zoho/cases")]
    public async Task<IActionResult> GetCases()
    {
        var cases = await _zohoOAuthService.GetZohoCasesAsync();
        return Ok(cases);
    }

    [HttpGet("zoho/{companyId}")]
    public async Task<IActionResult> GetCompanyInfo(string companyId)
    {
        var cases = await _zohoOAuthService.GetZohoCompanyAsync(companyId);
        return Ok(cases);
    }

    [HttpGet("zoho/{companyId}/photo")]
    public async Task<IActionResult> GetCompanyPhoto(string companyId)
    {
        var photo = await _zohoOAuthService.GetZohoAccountPhotoAsync(companyId);
        if (photo == null)
        {
            return NotFound();
        }

        return File(photo.Value.Content, photo.Value.ContentType);
    }

    [HttpGet("zoho/cases/{companyId}")]
    public async Task<IActionResult> GetCasesByCompany(string companyId)
    {
        var cases = await _zohoOAuthService.GetZohoCasesByCompanyAsync(companyId);
        return Ok(cases);
    }

    [HttpGet("zoho/cases/dashboard/{companyId}")]
    public async Task<IActionResult> GetCasesByCompanyForDashboard(string companyId)
    {
        var cases = await _zohoOAuthService.GetZohoCasesByCompanyForDahsboardAsync(companyId);
        return Ok(cases);
    }

    [HttpGet("zoho/contact/{email}")]
    public async Task<IActionResult> GetContactByEmail(string email)
    {
        var cases = await _zohoOAuthService.GetZohoContactByEmailAsync(email);
        return Ok(cases);
    }

    [HttpGet("zoho/case/{companyId}/{caseNumber}")]
    public async Task<IActionResult> GetCaseById(string companyId, string caseNumber)
    {
        var cases = await _zohoOAuthService.GetZohoCaseByIdAsync(companyId, caseNumber);
        return Ok(cases);
    }

    [HttpGet("zoho/case/{companyId}/{caseNumber}/timeline")]
    public async Task<IActionResult> GetCaseTimeline(string companyId, string caseNumber)
    {
        var timeline = await _zohoOAuthService.GetZohoCaseTimelineAsync(caseNumber);
        if (timeline == null)
        {
            return StatusCode(502, new { message = "Unable to reach Zoho." });
        }

        return new ContentResult
        {
            Content = timeline.Value.Body,
            ContentType = "application/json",
            StatusCode = timeline.Value.StatusCode
        };
    }

    [HttpPost("zoho/case/{companyId}/{caseNumber}/context")]
    public async Task<IActionResult> AddCaseContext(string companyId, string caseNumber, [FromBody] CaseContextRequest? request)
    {
        var result = await _zohoOAuthService.AddZohoCaseContextAsync(companyId, caseNumber, request?.Comments);
        if (result == null)
        {
            return StatusCode(502, new { message = "Unable to reach Zoho." });
        }

        if (result.StatusCode == 200)
        {
            try
            {
                await _emailService.SendCaseContextUpdateAsync(result.CaseId, result.CaseName, result.Status, result.Context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Case context email failed: {ex.Message}");
                return new ContentResult
                {
                    Content = "{\"message\":\"Context was saved, but the support email could not be sent.\"}",
                    ContentType = "application/json",
                    StatusCode = 502
                };
            }
        }

        return new ContentResult
        {
            Content = result.Body,
            ContentType = "application/json",
            StatusCode = result.StatusCode
        };
    }

    [HttpGet("zoho/website/{companyId}")]
    public async Task<IActionResult> GetWebsiteByCompany(string companyId)
    {
        var cases = await _zohoOAuthService.GetZohoWebsiteByCompanyAsync(companyId);
        return Ok(cases);
    }

    [HttpPost("zoho/newcase")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> CreateCase()
    {
        CaseModel? caseObj;
        var attachments = new List<IFormFile>();

        if (Request.HasFormContentType)
        {
            var caseDataJson = Request.Form["caseData"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(caseDataJson))
            {
                return BadRequest(new { message = "caseData is required." });
            }

            caseObj = System.Text.Json.JsonSerializer.Deserialize<CaseModel>(
                caseDataJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            attachments = Request.Form.Files
                .Where(f => string.Equals(f.Name, "attachments", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest(new { message = "Request body is required." });
            }

            caseObj = System.Text.Json.JsonSerializer.Deserialize<CaseModel>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        if (caseObj == null)
        {
            return BadRequest(new { message = "Invalid case data." });
        }

        var attachmentError = ValidateCaseAttachments(attachments);
        if (attachmentError != null)
        {
            return BadRequest(new { message = attachmentError });
        }

        var cases = await _zohoOAuthService.SaveZohoCasesAsync(
            caseObj,
            attachments.Count > 0 ? attachments : null);
        return Content(cases, "application/json");
    }

    private static string? ValidateCaseAttachments(IReadOnlyList<IFormFile> attachments)
    {
        const int maxFiles = 5;
        const long maxFileSizeBytes = 25 * 1024 * 1024;

        if (attachments.Count > maxFiles)
        {
            return $"A maximum of {maxFiles} attachments is allowed.";
        }

        foreach (var file in attachments)
        {
            if (file.Length > maxFileSizeBytes)
            {
                return $"Each attachment must be {maxFileSizeBytes / (1024 * 1024)} MB or smaller.";
            }
        }

        return null;
    }

    [HttpDelete("zoho/deleteCase/{id}")]
    public async Task<IActionResult> DeleteUserAsync(string id)
    {
        var cases = await _zohoOAuthService.DeleteZohoCasesAsync(id);
        return Ok(cases);
    }

    [HttpGet("zoho/deliverables/{companyId}")]
    public async Task<IActionResult> GetDeliverables(string companyId)
    {
        var deliverables = await _zohoOAuthService.GetZohoDeliverablesByCompanyAsync(companyId);
        return Ok(deliverables);
    }

    [HttpGet("zoho/deliverables/{companyId}/year")]
    public async Task<IActionResult> GetDeliverablesByAccountAndYear(string companyId, [FromQuery] int? year)
    {
        if (string.IsNullOrWhiteSpace(companyId) || !long.TryParse(companyId, out _))
        {
            return BadRequest(new { message = "A numeric Zoho account id is required." });
        }

        var targetYear = year ?? DateTime.Now.Year;
        if (targetYear < 2000 || targetYear > 2100)
        {
            return BadRequest(new { message = "Year must be between 2000 and 2100." });
        }

        var deliverables = await _zohoOAuthService.GetZohoDeliverablesByAccountAndYearAsync(companyId, targetYear);
        if (deliverables == null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Zoho CRM token is invalid." });
        }

        return new ContentResult
        {
            StatusCode = deliverables.Value.StatusCode,
            Content = deliverables.Value.Body,
            ContentType = "application/json"
        };
    }

    [HttpGet("zoho/deliverables/{companyId}/{blockId}/{status}")]
    public async Task<IActionResult> GetDeliverablesByBlock(string companyId, string blockId, string status)
    {
        var status_field = "";
        if(status=="pending")
        {
            status_field = "Client Approval";
        } else
        {
            if(status=="upcoming")
            {
                status_field = "Idea";
            } else
            {
                if(status=="completed")
                {
                    status_field = "Completed";
                } else
                {
                    status_field = "";
                }
            }
        }
        var deliverables = await _zohoOAuthService.GetZohoDeliverablesByCompanyAndBlockAsync(companyId, blockId, status_field);
        return Ok(deliverables);
    }

    [HttpPost("zoho/deliverables/{deliverableId}/{status}")]
    public async Task<IActionResult> updateDeliverableClientApproval(string deliverableId, string status, [FromBody] DeliverableDto deliverableDto)
    {
        var deliverables = await _zohoOAuthService.UpdateDeliverableClientApprovalAsync(deliverableId, status, deliverableDto.message);
        var email_body = $"Deliverable : {deliverableDto.name}<br/>" +
                 $"Company Name : {deliverableDto.company}<br/>" +
                 $"Deliverable Link : <a href='https://crm.zoho.com/crm/org668627929/tab/CustomModule22/{deliverableId}'>ZOho Deliverable Link</a><br/>" +
                 $"Client Message : {deliverableDto.message}<br/>" +
                 $"Approval Status : {status}";
        await _emailService.SendClientApprovalAsync(status, email_body, deliverableId);
        return Ok(deliverables);
    }

    [HttpPost("zoho/checkEmail")]
    public async Task<IActionResult> checkEmail([FromBody] EmailModel email)
    {
        var locked = await AccountLockedResultAsync(email.Email);
        if (locked != null)
        {
            return locked;
        }

        var response = new ApiResponse();
        var zohoAccountDetails = await _zohoOAuthService.checkEmail(email.Email);
        if(zohoAccountDetails == "")
        {
            response = new ApiResponse(true, "Email is not valid, please contact your admin.", "", "", false, false);
        } else
        {
            JObject jsonObj = JObject.Parse(zohoAccountDetails);

            Console.WriteLine(jsonObj);
            var account = jsonObj["data"][0]["Account_Name"];
            string companyname = account["name"].ToString();
            string companyId = account["id"].ToString();
            string fullname = jsonObj["data"][0]["Full_Name"].ToString();

            var userData = await _userService.IsEmailVerifiedAsync(email.Email);
            if(userData == null || userData.OTPStatus == false)
            {
                if (email.logintype == "" || email.logintype == null)
                {
                    Random random = new Random();
                    var otp = random.Next(100000, 999999).ToString();

                    await _emailService.SendOtpAsync(email.Email, otp);
                    await _userService.SaveEmailAndOtp(email.Email, otp, companyname, companyId, fullname);

                    response = new ApiResponse(true, "Email is not verified and OTP for email verification sent successfully.", "", zohoAccountDetails, true, false);
                }
                else
                {
                    var auth0AccountId = email.user;
                    var jsonDocument = JsonDocument.Parse(email.user);

                    // Extract the "sub" value
                    string sub = jsonDocument.RootElement.GetProperty("sub").GetString();
                    var user = new User
                    {
                        Email = email.Email,
                        Otp = "",
                        CompanyName = account["name"].ToString(),
                        CompanyId = account["id"].ToString(),
                        Password = "",
                        ZohoEmailStatus = true,
                        OTPStatus = true,
                        Auth0Status = true,
                        Auth0AccountId = sub,
                        first_name = jsonObj["data"][0]["First_Name"].ToString(),
                        full_name = jsonObj["data"][0]["Full_Name"].ToString()
                    };
                    await _userService.SaveUser(user);

                    response = new ApiResponse(true, "", JsonConvert.SerializeObject(user), JsonConvert.SerializeObject(zohoAccountDetails), true, true);
                }
                return Ok(response);
            } else
            {
                response.Obj = JsonConvert.SerializeObject(userData);
                response.Zoho = JsonConvert.SerializeObject(zohoAccountDetails);
                response.EmailVerified = true;
                response.OtpVerified = userData.OTPStatus;
            }
        }
        return Ok(response);
    }

    [HttpPost("zoho/verifyOTP")]
    public async Task<IActionResult> verifyOTP([FromBody] EmailModel email)
    {
        var userData = await _userService.verifyOTP(email.Email, email.otp, email.password);
        var response = new ApiResponse();
        if (userData != null)
        {
            response = new ApiResponse(true, "Email OTP verified.", userData.ToString(), "", userData.ZohoEmailStatus, userData.OTPStatus);
            return Ok(response);
        } else
        {
            response = new ApiResponse(true, "Email is verified", "", "", true, true);
        }
        return Ok(response);
    }

    [HttpPost("zoho/login")]
    public async Task<IActionResult> login([FromBody] EmailModel email)
    {
        var locked = await AccountLockedResultAsync(email.Email);
        if (locked != null)
        {
            return locked;
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var userData = await _userService.login(email.Email, email.password);
        if (userData != null)
        {
            var auth0TokenObj = await _auth0Service.Auth0UserLogin(email.Email, email.password);
            if (auth0TokenObj != null)
            {
                await _userService.RecordLoginActivityAsync(
                    email.Email,
                    "Login",
                    "Success",
                    "User logged in successfully.",
                    ipAddress,
                    userAgent);

                // Get the current date and time
                DateTime currentDateTime = DateTime.Now;

                // Add 8400 seconds (2 hours and 20 minutes) to the current date and time
                DateTime expirationDateTime = currentDateTime.AddSeconds(auth0TokenObj.expires_in);

                auth0TokenObj.ExpiresIn = expirationDateTime;
                auth0TokenObj.Auth0AccountId = userData.Auth0AccountId;
                auth0TokenObj.Auth0Status = userData.Auth0Status;
                auth0TokenObj.CompanyId = userData.CompanyId;
                auth0TokenObj.CompanyName = userData.CompanyName;
                auth0TokenObj.Email = userData.Email;
                auth0TokenObj.Username = userData.full_name;
                return Ok(auth0TokenObj);
            }

            var authFailure = await RecordFailedLoginAsync(
                email.Email,
                "Login",
                "Auth0 authentication failed.",
                ipAddress,
                userAgent,
                sendAlert: true);
            return authFailure ?? NoContent();
        }

        var unknownUser = await RecordFailedLoginAsync(
            email.Email,
            "Login",
            "User not found or not eligible for login.",
            ipAddress,
            userAgent,
            sendAlert: false);
        return unknownUser ?? NoContent();
    }

    [HttpPost("zoho/logout")]
    public async Task<IActionResult> Logout([FromBody] EmailModel email)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            return BadRequest(new { message = "Email is required for logout activity." });
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        await _userService.RecordLoginActivityAsync(
            email.Email,
            "Logout",
            "Success",
            "User logged out.",
            ipAddress,
            userAgent);

        return Ok(new { message = "Logout activity recorded successfully." });
    }

    [HttpPost("zoho/login-failure")]
    public async Task<IActionResult> RecordSocialLoginFailure([FromBody] EmailModel emailModel)
    {
        if (string.IsNullOrWhiteSpace(emailModel.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();
        var provider = string.IsNullOrWhiteSpace(emailModel.logintype) ? "Social" : emailModel.logintype;
        var details = string.IsNullOrWhiteSpace(emailModel.details)
            ? $"{provider} login failed."
            : emailModel.details;

        var locked = await RecordFailedLoginAsync(
            emailModel.Email,
            $"{provider}Login",
            details,
            ipAddress,
            userAgent,
            sendAlert: true);
        if (locked != null)
        {
            return locked;
        }

        return Ok(new { message = "Failed login attempt recorded." });
    }

    [HttpPost("zoho/register")]
    public async Task<IActionResult> register([FromBody] EmailModel email)
    {
        var zohoAccountDetails = await _zohoOAuthService.checkEmail(email.Email);
        var result = await _auth0Service.checkEmailInAuth0(email.Email);
        if (!result)
        {
            JObject jsonObj = JObject.Parse(zohoAccountDetails);
            var account = jsonObj["data"][0]["Account_Name"];
            var user = new User
            {
                Email = email.Email,
                Otp = email.otp,
                CompanyName = account["name"].ToString(),
                CompanyId = account["id"].ToString(),
                Password = email.password,
                ZohoEmailStatus = true,
                OTPStatus = false,
                Auth0Status = false,
                first_name = jsonObj["data"][0]["First_Name"].ToString(),
                full_name = jsonObj["data"][0]["Full_Name"].ToString()
            };
            Console.WriteLine(jsonObj);
            //var userStatus = await _auth0Service.createUserInAuth0Admin(user);
            var userStatus = await _auth0Service.createUserInAuth0(email.Email, email.password, jsonObj);
            JObject userAuthStatus = JObject.Parse(userStatus);
            user.Auth0Status = true;
            user.Auth0AccountId = userAuthStatus["user_id"].ToString();
            var updateUser = await _userService.updateUser(email.Email, email.password, userAuthStatus);
            //Console.WriteLine(updateUser);
            var res = new ApiResponse(true, "registered.", userAuthStatus.ToString(), updateUser.ToString(), true, true);
            return Ok(res);
        }
        else
        {
            var res = new ApiResponse(true, "Registration Failed", "", "", true, true);
            return BadRequest(res);
        }
    }

    [HttpGet("zoho/companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var deliverables = await _zohoOAuthService.GetZohoCompanies();
        return Ok(deliverables);
    }

    [HttpPost("zoho/checkEmailForgotPassword")]
    public async Task<IActionResult> checkEmailForgotPassword([FromBody] EmailModel email)
    {
        var response = new ApiResponse();
        var zohoAccountDetails = await _zohoOAuthService.checkEmail(email.Email);
        if (zohoAccountDetails == "")
        {
            response = new ApiResponse(true, "Email is not valid, please contact your admin.", "", "", false, false);
        }
        else
        {
            JObject jsonObj = JObject.Parse(zohoAccountDetails);

            Console.WriteLine(jsonObj);
            var account = jsonObj["data"][0]["Account_Name"];
            string companyname = account["name"].ToString();
            string companyId = account["id"].ToString();
            string fullname = jsonObj["data"][0]["Full_Name"].ToString();

            var userData = await _userService.IsEmailVerifiedAsync(email.Email);
            if (userData != null)
            {
                Random random = new Random();
                var otp = random.Next(100000, 999999).ToString();
                await _emailService.SendOtpAsync(email.Email, otp);
                await _userService.SaveEmailAndOtp(email.Email, otp, companyname, companyId, fullname);

                response = new ApiResponse(true, "OTP for email verification sent successfully.", "", zohoAccountDetails, true, false);
            }
            else
            {
                response = new ApiResponse(true, "Email not registered.", "", zohoAccountDetails, true, false);
            }
        }
        return Ok(response);
    }

    [HttpPost("zoho/verifyOTPUpdatePassword")]
    public async Task<IActionResult> verifyOTPUpdatePassword([FromBody] EmailModel email)
    {
        var response = new ApiResponse();
        var zohoAccountDetails = await _zohoOAuthService.checkEmail(email.Email);
        if (zohoAccountDetails == "")
        {
            response = new ApiResponse(true, "Email is not valid, please contact your admin.", "", "", false, false);
        }
        else
        {
            JObject jsonObj = JObject.Parse(zohoAccountDetails);

            Console.WriteLine(jsonObj);
            var account = jsonObj["data"][0]["Account_Name"];
            string companyname = account["name"].ToString();
            string companyId = account["id"].ToString();
            string fullname = jsonObj["data"][0]["Full_Name"].ToString();

            var userData = await _userService.verifyOTP(email.Email, email.otp, email.password);
            if (userData != null)
            {
                var userStatus = await _auth0Service.UpdatePasswordAsync(userData.Auth0AccountId, email.password);
                JObject userAuthStatus = JObject.Parse(userStatus);
                await _userService.updateUser(email.Email, email.password, userAuthStatus);
                Random random = new Random();
                var otp = random.Next(100000, 999999).ToString();
                await _emailService.SendOtpAsync(email.Email, otp);
                await _userService.SaveEmailAndOtp(email.Email, otp, companyname, companyId, fullname);

                response = new ApiResponse(true, "OTP verified and Password updated successfully.", "", zohoAccountDetails, true, false);
            }
            else
            {
                response = new ApiResponse(true, "OTP Verification and Password update failed.", "", zohoAccountDetails, true, false);
            }
        }
        return Ok(response);
    }

    private async Task<IActionResult?> AccountLockedResultAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var lockout = await _userService.GetActiveLockoutAsync(email, LockoutMinutes);
        if (!lockout.IsLocked)
        {
            return null;
        }

        return AccountLocked(lockout.RetryAfterMinutes);
    }

    private IActionResult AccountLocked(int retryAfterMinutes)
    {
        var minutes = Math.Max(1, retryAfterMinutes);
        var unit = minutes == 1 ? "minute" : "minutes";
        Response.Headers.RetryAfter = (minutes * 60).ToString();
        return StatusCode(StatusCodes.Status423Locked, new
        {
            locked = true,
            message = $"Your account is locked after too many failed login attempts. Try again in {minutes} {unit}.",
            retryAfterMinutes = minutes
        });
    }

    private async Task<IActionResult?> RecordFailedLoginAsync(
        string email,
        string activityType,
        string details,
        string? ipAddress,
        string? userAgent,
        bool sendAlert)
    {
        var alreadyLocked = await AccountLockedResultAsync(email);
        if (alreadyLocked != null)
        {
            return alreadyLocked;
        }

        await _userService.RecordLoginActivityAsync(
            email,
            activityType,
            "Failed",
            details,
            ipAddress,
            userAgent);

        var failedCount = await _userService.GetRecentFailedLoginCountAsync(email, FailedWindowMinutes);
        if (failedCount < FailedLoginThreshold)
        {
            return null;
        }

        await _userService.RecordLoginActivityAsync(
            email,
            "AccountLockout",
            "Locked",
            $"Account locked for {LockoutMinutes} minutes after {failedCount} failed attempts.",
            ipAddress,
            userAgent);

        if (sendAlert)
        {
            var alreadyNotified = await _userService.HasRecentFailureAlertAsync(email, FailedWindowMinutes);
            if (!alreadyNotified)
            {
                try
                {
                    await _emailService.SendFailedLoginAlertAsync(email, failedCount, FailedWindowMinutes, LockoutMinutes);
                    await _userService.RecordLoginActivityAsync(
                        email,
                        "FailedLoginAlert",
                        "Notified",
                        $"Alert email sent after {failedCount} failed attempts in {FailedWindowMinutes} minutes. Account locked for {LockoutMinutes} minutes.",
                        ipAddress,
                        userAgent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send lockout alert for {email}: {ex.Message}");
                }
            }
        }

        return AccountLocked(LockoutMinutes);
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers.UserAgent.ToString();
    }
}