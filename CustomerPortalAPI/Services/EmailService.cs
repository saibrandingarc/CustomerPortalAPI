using System;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;
using MailKit.Search;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using static System.Net.WebRequestMethods;
using CustomerPortalAPI.Models;
using MailKit.Net.Imap;
using Intuit.Ipp.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Net;
//using System.Net.Mail;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using Microsoft.Extensions.Options;
using CustomerPortalAPI.Controllers;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SendGrid.Helpers.Mail.Model;
using MailKit;
using static MailKit.Net.Imap.ImapMailboxFilter;
using System.Text;
using MimeKit;
using MailKit.Net.Smtp;

namespace CustomerPortalAPI.Services
{
    public class EmailService
    {
        private readonly string _sendGridApiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly IConfiguration _configuration;
        private readonly ZohoServices _zohoOAuthService;

        public EmailService(IConfiguration configuration, ZohoServices zohoOAuthService)
        {
            _configuration = configuration;
            _sendGridApiKey = configuration["SendGrid:ApiKey"];
            _fromEmail = configuration["SendGrid:FromEmail"];
            _fromName = configuration["SendGrid:FromName"];
            _zohoOAuthService = zohoOAuthService;
        }

        // Method to generate a 6-digit OTP
        public string GenerateOtp()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString(); // Generates a number between 100000 and 999999
        }

        // Method to send the OTP via email
        public async Task SendOtpAsync(string toEmail, string otp, bool isHtml = true)
        {
            try
            {
                string GmailHost = _configuration["emailsendconfig:GmailImapHost"];
                int GmailImapPort = int.Parse(_configuration["emailsendconfig:GmailImapPort"]);

                string GmailUsername = _configuration["emailsendconfig:GmailUsername"];
                string GmailAppPassword = _configuration["emailsendconfig:GmailAppPassword"];

                // Create the email message
                var mime_message = new MimeMessage();
                mime_message.From.Add(new MailboxAddress("Mailer", GmailUsername));
                mime_message.To.Add(MailboxAddress.Parse(toEmail));
                mime_message.Subject = "Your OTP Code";
                var message = $"Your OTP is: {otp}";

                // Set body type (HTML or plain text)
                mime_message.Body = new TextPart(isHtml ? "html" : "plain") { Text = message };

                // Send email using Gmail SMTP
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(GmailHost, GmailImapPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(GmailUsername, GmailAppPassword);
                await smtp.SendAsync(mime_message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send approval email: {ex.Message}", ex);
            }
            //var client = new SendGridClient(_sendGridApiKey);
            //var from = new EmailAddress(_fromEmail, _fromName);
            //var subject = "Your OTP Code";
            //var to = new EmailAddress(toEmail);
            //var plainTextContent = $"Your OTP is: {otp}";
            //var htmlContent = $"<strong>Your OTP is: {otp}</strong>";

            //var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            //try
            //{
            //    var response = await client.SendEmailAsync(msg);
            //    if (response.StatusCode != System.Net.HttpStatusCode.OK &&
            //        response.StatusCode != System.Net.HttpStatusCode.Accepted)
            //    {
            //        var body = await response.Body.ReadAsStringAsync();
            //        throw new Exception($"Failed to send email. Status code: {response.StatusCode}. Response body: {body}");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    throw new Exception($"Failed to send OTP: {ex.Message}", ex);
            //}
        }

        public async Task SendEmailAsync(string toEmail, object data)
        {
            var client = new SendGridClient(_sendGridApiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var subject = "Case Id :";
            var to = new EmailAddress(toEmail);
            var plainTextContent = $"Data: {data}";
            var htmlContent = $"<strong>Data: {data}</strong>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            try
            {
                var response = await client.SendEmailAsync(msg);
                if (response.StatusCode != System.Net.HttpStatusCode.OK &&
                    response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    var body = await response.Body.ReadAsStringAsync();
                    throw new Exception($"Failed to send email. Status code: {response.StatusCode}. Response body: {body}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send OTP: {ex.Message}", ex);
            }
        }

        public async Task<List<EmailDto>> GetInboxEmails()
        {
            var emails = new List<EmailDto>();
            string GmailImapHost = _configuration["emailconfig:GmailImapHost"];
            int GmailImapPort = int.Parse(_configuration["emailconfig:GmailImapPort"]);

            string GmailUsername = _configuration["emailconfig:GmailUsername"];
            string GmailAppPassword = _configuration["emailconfig:GmailAppPassword"];

            try
            {
                using (var client = new ImapClient())
                {
                    await client.ConnectAsync(GmailImapHost, GmailImapPort, SecureSocketOptions.SslOnConnect);
                    await client.AuthenticateAsync(GmailUsername, GmailAppPassword);

                    // Access the Inbox folder
                    var inbox = client.Inbox;
                    await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

                    // Fetch the last 10 emails
                    var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
                    foreach (var uid in uids.TakeLast(10))
                    {
                        var message = await inbox.GetMessageAsync(uid);
                        emails.Add(new EmailDto
                        {
                            Subject = message.Subject,
                            From = string.Join(", ", message.From.Select(f => f.ToString())),
                            Date = message.Date.LocalDateTime,
                            Body = message.TextBody ?? message.HtmlBody
                        });
                    }

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //return StatusCode(500, $"Error reading emails: {ex.Message}");
            }

            return emails;
        }

        public async Task<string> GetSentEmails(string token)
        {
            var GmailToken = _configuration["emailconfig:token"];
            if (token == GmailToken)
            {
                string label = "evaluated";
                try
                {
                    using (var client = new ImapClient())
                    {
                        var sentEmails = new List<string>();

                        string GmailImapHost = _configuration["emailconfig:GmailImapHost"];
                        int GmailImapPort = int.Parse(_configuration["emailconfig:GmailImapPort"]);

                        string GmailUsername = _configuration["emailconfig:GmailUsername"];
                        string GmailAppPassword = _configuration["emailconfig:GmailAppPassword"];

                        await client.ConnectAsync(GmailImapHost, GmailImapPort, SecureSocketOptions.SslOnConnect);
                        await client.AuthenticateAsync(GmailUsername, GmailAppPassword);

                        // Get Sent Mail folder (use SpecialFolder.Sent for automatic mapping)
                        var sentFolder = client.GetFolder(SpecialFolder.Sent);

                        // Open folder in read-only mode
                        await sentFolder.OpenAsync(FolderAccess.ReadWrite);

                        // Get the latest 10 sent emails
                        int totalMessages = sentFolder.Count;
                        int fetchCount = Math.Min(10, totalMessages); // Get last 10 messages
                        var messages = await sentFolder.FetchAsync(totalMessages - fetchCount, totalMessages - 1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.GMailLabels);

                        foreach (var messageSummary in messages)
                        {
                            // Fetch the full message to get body content
                            var message = await sentFolder.GetMessageAsync(messageSummary.UniqueId);

                            // Extract email details
                            string senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address;
                            string recipientEmail = message.To.Mailboxes.FirstOrDefault()?.Address;
                            string subject = message.Subject;
                            string plainTextBody = message.TextBody;  // Plain text version
                            string htmlBody = message.HtmlBody;  // HTML version
                            DateTime sentDate = message.Date.UtcDateTime;

                            var labels = messageSummary.GMailLabels;
                            if (labels.Contains(label, StringComparer.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("The array contains the label 'evaluated'.");
                            }
                            else
                            {
                                if (messageSummary.UniqueId != null)
                                {
                                    await sentFolder.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Flagged, default);
                                    await sentFolder.AddLabelsAsync(messageSummary.UniqueId, new[] { label, "Seen" }, true, default);

                                    Console.WriteLine($"Label '{label}' added to email with UID: {messageSummary.UniqueId}");
                                }

                                if (subject.Contains("Case Id") || subject.Contains("case id"))
                                {
                                    var parts = subject.Split(':');

                                    //// Handle the split parts
                                    if (parts.Length > 1)
                                    {
                                        var id = parts[2].Trim();
                                        var fromEmail = recipientEmail;
                                        var contact = await _zohoOAuthService.GetZohoContactByEmailAsync(fromEmail);
                                        if (contact == null)
                                        {
                                            Console.WriteLine("not exist");
                                            return "FAILURE";
                                        }
                                        else
                                        {
                                            Console.WriteLine("exist");
                                            var parsedJson = JObject.Parse(contact)["data"][0];
                                            var acc = new Account { id = parsedJson["Account_Name"]["id"].ToString() };
                                            var caseData = await _zohoOAuthService.GetZohoCaseByIdAsync(parsedJson["Account_Name"]["id"].ToString(), id);
                                            Console.WriteLine(caseData);
                                            if (caseData == "" || caseData == null)
                                            {
                                                Console.WriteLine("Case not exist");
                                                var result = new
                                                {
                                                    Code = 404,
                                                    Message = "no data exist with the case id"
                                                };
                                                return System.Text.Json.JsonSerializer.Serialize(result);
                                            }
                                            else
                                            {
                                                Console.WriteLine("Case exist");
                                                using JsonDocument document = JsonDocument.Parse(caseData);
                                                var rootElement = document.RootElement.GetProperty("data")[0];
                                                //JsonElement firstDataElement = root.GetProperty("data");
                                                CaseModel caseObj = System.Text.Json.JsonSerializer.Deserialize<CaseModel>(rootElement.GetRawText());
                                                Console.WriteLine(caseObj);
                                                string normalizedContent = plainTextBody.Replace("\r\n", "\n").Replace("\r", "\n");

                                                //// Define regex pattern
                                                string pattern = @"(?s)(On\s.*?wrote:.*$)|(--\s*$)|(From:\s.*?$)";

                                                //// Clean email content
                                                string emailBody = Regex.Replace(normalizedContent, pattern, string.Empty, RegexOptions.Multiline).Trim();
                                                var emailNote = new EmailNote
                                                {
                                                    Comment_Date = sentDate,
                                                    Comment_Type = "Admin",
                                                    Comments = emailBody,

                                                };
                                                List<EmailNote> _emailNotes = new();
                                                _emailNotes.Add(emailNote);
                                                caseObj.Email_Notes1 = _emailNotes;
                                                var zohoRequest = new ZohoCaseRequest
                                                {
                                                    data = new List<CaseModel> { caseObj }
                                                };
                                                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(zohoRequest);
                                                var cases = await _zohoOAuthService.UpdateZohoCasesAsync(jsonPayload, id);
                                                if (messageSummary.UniqueId != null)
                                                {
                                                    await sentFolder.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Flagged, default);
                                                    await sentFolder.AddLabelsAsync(messageSummary.UniqueId, new[] { label, "Seen" }, true, default);
                                                    return $"Label '{label}' added to email with UID: {messageSummary.UniqueId}";
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var result = new
                                        {
                                            Code = 200,
                                            CaseId = parts[1].Trim(),
                                            Message = "Existing Case Id Issue"
                                        };
                                        return System.Text.Json.JsonSerializer.Serialize(result);
                                    }
                                }
                                else
                                {
                                    var result = new
                                    {
                                        Code = 200,
                                        CaseId = "",
                                        Message = "New Case Id Issue GetSentEmails"
                                    };
                                    return System.Text.Json.JsonSerializer.Serialize(result);
                                }
                            }
                        }
                        await client.DisconnectAsync(true);
                        return "SUCCESS";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return ex.Message;
                }
            } else
            {
                return "FAILURE";
            }
        }

        public async Task<string> SendEmail(string toEmail, object data, string subjectContent, string message, string messageid)
        {
            var client = new SendGridClient(_sendGridApiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var subject = subjectContent;
            var to = new EmailAddress(toEmail);
            var plainTextContent = message;
            var htmlContent = message;

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            msg.SetReplyTo(from);
            // Add headers for threading
            if (!string.IsNullOrWhiteSpace(messageid))
            {
                msg.Headers = new Dictionary<string, string>
                {
                    { "In-Reply-To", "<"+messageid+"@mail.gmail.com>" },
                    { "References", "<"+messageid+"@mail.gmail.com>" }
                };
            }
            else
            {
                Console.WriteLine("Original Message ID is missing or invalid.");
            }
            try
            {
                var response = await client.SendEmailAsync(msg);
                if (response.StatusCode != System.Net.HttpStatusCode.OK &&
                    response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    var body = await response.Body.ReadAsStringAsync();
                    throw new Exception($"Failed to send email. Status code: {response.StatusCode}. Response body: {body}");
                }
                return $"Failed to send OTP: {response}";
            }
            catch (Exception ex)
            {
                return $"Failed to send OTP: {ex.Message}";
                //throw new Exception($"Failed to send OTP: {ex.Message}", ex);

            }
        }

        public async Task<object> EmailInboxWebhook(EmailInboxModel request)
        {
            if(request.FromEmail == "")
            {
                return new { status_code = 404, message = "email address not exist" };
            } else
            {
                var contact = await _zohoOAuthService.GetZohoContactByEmailAsync(request.FromEmail.ToString());
                if(contact == null)
                {
                    return new { status_code = 204, message = "email contact not exist" };
                } else
                {
                    // Sample subject
                    string subject = request.Subject;

                    // Regex to check for 8 or more digit number
                    bool hasLongNumber = Regex.IsMatch(subject, @"\d{8,}");
                    if (!hasLongNumber)
                    {
                        //return new { status_code = 204, message = "inside if" + hasLongNumber };
                        var parsedJson = JObject.Parse(contact)["data"][0];
                        var acc = new Account {id = parsedJson["Account_Name"]["id"].ToString() };
                        var emailNote = new EmailNote
                        {
                            Comment_Date = request.Date,
                            Comment_Type = "Client",
                            Comments = request.BodyPlain,

                        };
                        List<EmailNote> _emailNotes = new();
                        _emailNotes.Add(emailNote);
                        var caseModel = new CaseModel
                        {
                            Account_Name = acc,
                            Status = "success",
                            Email = request.FromEmail,
                            Description = request.BodyPlain,
                            Subject = request.Subject,
                            Email_Notes1 = _emailNotes,
                            Case_Origin = "Email"
                        };

                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true, // For pretty-print JSON
                            Converters = { new NullableCustomDateTimeConverter() } // Custom DateTime converter if needed
                        };

                        string jsonString = System.Text.Json.JsonSerializer.Serialize(caseModel, options);
                        Console.WriteLine(jsonString);
                        var cases = await _zohoOAuthService.SaveZohoCasesAsync(caseModel);
                        
                        using (JsonDocument doc = JsonDocument.Parse(cases))
                        {
                            var dataElement = doc.RootElement.GetProperty("data")[0];
                            var code = dataElement.GetProperty("code").GetString();
                            var detailsId = dataElement.GetProperty("details").GetProperty("id").GetString();
                            // Step 3: Iterate and concatenate strings
                            string CommentsList = "";

                            foreach (var note in _emailNotes)
                            {
                                CommentsList += $"{note.Comment_Date:yyyy-MM-dd} [{note.Comment_Type}]: {note.Comments}\n";
                            }

                            var result = new
                            {
                                Code = code,
                                CaseId = detailsId,
                                Message = "Case Description : " + CommentsList + "\nNew Case Created. Please reply to the same email for further communication about this case."
                            };
                            return result;
                        }
                    }
                    else
                    {
                        //return new { status_code = 204, message = "inside else" + subject };
                        var parts = request.Subject.Split(':');

                        //// Handle the split parts
                        if (parts.Length > 1)
                        {
                            var id = parts[2].Trim();
                            var parsedJson = JObject.Parse(contact)["data"][0];
                            var acc = new Account { id = parsedJson["Account_Name"]["id"].ToString() };
                            var caseData = await _zohoOAuthService.GetZohoCaseByIdAsync(parsedJson["Account_Name"]["id"].ToString(), id);
                            if(caseData == "" || caseData == null)
                            {
                                var result = new
                                {
                                    Code = 404,
                                    Message = "no data exist with the case id"
                                };
                                return result;
                            } else
                            {
                                // Parse the JSON string
                                using JsonDocument document = JsonDocument.Parse(caseData);
                                var rootElement = document.RootElement.GetProperty("data")[0];
                                //JsonElement firstDataElement = root.GetProperty("data");
                                CaseModel caseObj = System.Text.Json.JsonSerializer.Deserialize<CaseModel>(rootElement.GetRawText());


                                //var htmlDoc = new HtmlDocument();
                                //htmlDoc.LoadHtml(request.BodyHtml);

                                //// Select the first <div dir="ltr"> that contains text
                                //var div = htmlDoc.DocumentNode.SelectSingleNode("//div[@dir='ltr' and text()[normalize-space()]]");

                                //// Extract and print the content
                                //if (div != null)
                                //{
                                //    Console.WriteLine(div.InnerText.Trim());
                                //}
                                //else
                                //{
                                //    Console.WriteLine("No matching <div dir=\"ltr\"> found.");
                                //}

                                // Normalize line breaks
                                string normalizedContent = request.BodyPlain.Replace("\r\n", "\n").Replace("\r", "\n");

                                // Define regex pattern
                                string pattern = @"(?s)(On\s.*?wrote:.*$)|(--\s*$)|(From:\s.*?$)";

                                // Clean email content
                                string emailBody = Regex.Replace(normalizedContent, pattern, string.Empty, RegexOptions.Multiline).Trim();

                                //string pattern = @"(\r?\nOn\s.*?wrote:\r?\n[\s\S]*)|(--\s*\r?\n[\s\S]*)|(\r?\n>.*)";

                                //var emailBody = Regex.Replace(request.BodyPlain, pattern, "", RegexOptions.IgnoreCase).Trim();

                                var emailNote = new EmailNote
                                {
                                    Comment_Date = request.Date,
                                    Comment_Type = "Client",
                                    Comments = emailBody,

                                };
                                List<EmailNote> _emailNotes = new();
                                _emailNotes.Add(emailNote);
                                caseObj.Email_Notes1 = _emailNotes;
                                var zohoRequest = new ZohoCaseRequest
                                {
                                    data = new List<CaseModel> { caseObj }
                                };
                                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(zohoRequest);
                                var cases = await _zohoOAuthService.UpdateZohoCasesAsync(jsonPayload, id);
                                var result = new
                                {
                                    Code = 200,
                                    CaseData = emailBody,
                                    CaseId = id,
                                    Message = "Existing Case Id Updated"
                                };
                                return result;
                            }
                        }
                        else
                        {
                            var result = new
                            {
                                Code = 200,
                                CaseId = parts[1].Trim(),
                                Message = "Existing Case Id Issue"
                            };
                            return result;
                        }
                    }
                }
            }
            
            //var emails = new List<EmailDto>();
            //string GmailImapHost = _configuration["emailconfig:GmailImapHost"];
            //int GmailImapPort = int.Parse(_configuration["emailconfig:GmailImapPort"]);

            //string GmailUsername = _configuration["emailconfig:GmailUsername"];
            //string GmailAppPassword = _configuration["emailconfig:GmailAppPassword"];

            //try
            //{
            //    using (var client = new ImapClient())
            //    {
            //        await client.ConnectAsync(GmailImapHost, GmailImapPort, SecureSocketOptions.SslOnConnect);
            //        await client.AuthenticateAsync(GmailUsername, GmailAppPassword);

            //        // Access the Inbox folder
            //        var inbox = client.Inbox;
            //        await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

            //        // Fetch the last 10 emails
            //        var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
            //        foreach (var uid in uids.TakeLast(10))
            //        {
            //            var message = await inbox.GetMessageAsync(uid);
            //            emails.Add(new EmailDto
            //            {
            //                Subject = message.Subject,
            //                From = string.Join(", ", message.From.Select(f => f.ToString())),
            //                Date = message.Date.LocalDateTime,
            //                Body = message.TextBody ?? message.HtmlBody
            //            });
            //        }

            //        await client.DisconnectAsync(true);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //    //return StatusCode(500, $"Error reading emails: {ex.Message}");
            //}

            //return emails;
        }

        //public async Task SendReplyEmailAsync(string smtpHost, int smtpPort, string smtpUsername, string smtpPassword,
        //                                          string toEmail, string subject, string body, string originalMessageId)
        //{
        //    using (var client = new SmtpClient(smtpHost, smtpPort))
        //    {
        //        client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
        //        client.EnableSsl = true;

        //        var mailMessage = new MailMessage
        //        {
        //            From = new MailAddress(smtpUsername, "Customer Portal"),
        //            Subject = subject,
        //            Body = body,
        //            IsBodyHtml = true
        //        };

        //        // Add recipient
        //        mailMessage.To.Add(toEmail);

        //        // Add threading headers
        //        mailMessage.Headers.Add("In-Reply-To", originalMessageId);
        //        mailMessage.Headers.Add("References", originalMessageId);

        //        // Send the email
        //        await client.SendMailAsync(mailMessage);

        //        Console.WriteLine("Reply sent successfully.");
        //    }
        //}

        // Method to send the ClientApproval via email
        public async Task SendClientApprovalAsync(string status, string message, string deliverableId, bool isHtml = true)
        {
            try
            {
                string GmailHost = _configuration["emailsendconfig:GmailImapHost"];
                int GmailImapPort = int.Parse(_configuration["emailsendconfig:GmailImapPort"]);

                string GmailUsername = _configuration["emailsendconfig:GmailUsername"];
                string GmailAppPassword = _configuration["emailsendconfig:GmailAppPassword"];
                string toEmail = _configuration["emailsendconfig:defaultemail"];
                // Create the email message
                var mime_message = new MimeMessage();
                mime_message.From.Add(new MailboxAddress("Mailer", GmailUsername));
                mime_message.To.Add(MailboxAddress.Parse(toEmail));
                mime_message.Subject = $"Marketing Content - Client Approval Status - {status}";

                // Set body type (HTML or plain text)
                mime_message.Body = new TextPart(isHtml ? "html" : "plain") { Text = message };

                // Send email using Gmail SMTP
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(GmailHost, GmailImapPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(GmailUsername, GmailAppPassword);
                await smtp.SendAsync(mime_message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send approval email: {ex.Message}", ex);
            }
        }

        public async Task SendFailedLoginAlertAsync(string toEmail, int failedAttempts, int withinMinutes, int lockoutMinutes = 15, bool isHtml = true)
        {
            try
            {
                string GmailHost = _configuration["emailsendconfig:GmailImapHost"];
                int GmailImapPort = int.Parse(_configuration["emailsendconfig:GmailImapPort"]);

                string GmailUsername = _configuration["emailsendconfig:GmailUsername"];
                string GmailAppPassword = _configuration["emailsendconfig:GmailAppPassword"];

                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress("Security Alert", GmailUsername));
                mimeMessage.To.Add(MailboxAddress.Parse(toEmail));
                mimeMessage.Subject = "Security alert: multiple failed login attempts";

                var message =
                    $"We noticed {failedAttempts} failed login attempts on your account within the last {withinMinutes} minutes.<br/>" +
                    $"Your account is locked for {lockoutMinutes} minutes.<br/>" +
                    "If this was not you, reset your password immediately and contact support.";

                mimeMessage.Body = new TextPart(isHtml ? "html" : "plain") { Text = message };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(GmailHost, GmailImapPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(GmailUsername, GmailAppPassword);
                await smtp.SendAsync(mimeMessage);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send failed-login alert email: {ex.Message}", ex);
            }
        }
    }
}
