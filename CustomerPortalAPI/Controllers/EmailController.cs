using System;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CustomerPortalAPI.Services;
using MailKit.Search;
using MailKit.Security;
using CustomerPortalAPI.Models;
using MailKit.Net.Imap;
using static System.Net.WebRequestMethods;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace CustomerPortalAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase {

        private readonly EmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(EmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("sendOTP")]
        public async Task<IActionResult> SendEmail([FromBody] string email)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request");
            }

            try
            {
                Random random = new Random();
                var otp = random.Next(100000, 999999).ToString();

                await _emailService.SendOtpAsync(email, otp);
                return Ok("Email sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInboxEmails()
        {
            var emails = await _emailService.GetInboxEmails();
            return Ok(emails);
        }

        [HttpGet("sent/{token}")]
        public async Task<IActionResult> GetSentEmails(string token)
        {
            var emails = await _emailService.GetSentEmails(token);
            return Ok(emails);
        }

        [HttpPost("emailInboxWebhook")]
        public async Task<IActionResult> EmailInboxWebhook([FromBody] JsonElement request)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true, // Handle case-insensitive properties
                    Converters = { new NullableCustomDateTimeConverter() } // Add custom converter
                };

                var emailCase = JsonSerializer.Deserialize<EmailInboxModel>(request.GetRawText(), options);
                var labels = emailCase.Label.Split(',');
                var index = Array.FindIndex(labels, x => x == "UNREAD");
                if(index >= 0)
                {
                    var data = await _emailService.EmailInboxWebhook(emailCase);

                    PropertyInfo caseid = data.GetType().GetProperty("CaseId");
                    PropertyInfo message = data.GetType().GetProperty("Message");
                    Console.WriteLine(caseid?.GetValue(data));
                    string jsonString = JsonSerializer.Serialize(request);
                    if(message?.GetValue(data) != "Existing Case Id Updated")
                    {
                        await _emailService.SendEmail(emailCase.FromEmail, jsonString, "Case Id: " + caseid?.GetValue(data), message?.GetValue(data).ToString(), emailCase.MessageId);
                    }
                    //var data = await _emailService.SendEmail(emailCase.FromEmail, jsonString, "Case Id: 1234", "test email", "1");
                    return Ok(data);
                }
                else
                {
                    return Ok(index);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}