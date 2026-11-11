using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortalAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeliverableFileApprovalController : ControllerBase
    {
        private readonly DeliverableFileApprovalService _approvalService;
        private readonly ZohoServices _zohoService;
        private readonly EmailService _emailService;
        private readonly ILogger<DeliverableFileApprovalController> _logger;

        public DeliverableFileApprovalController(
            DeliverableFileApprovalService approvalService,
            ZohoServices zohoService,
            EmailService emailService,
            ILogger<DeliverableFileApprovalController> logger)
        {
            _approvalService = approvalService;
            _zohoService = zohoService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("{deliverableId}/{status}")]
        public async Task<IActionResult> SaveFileApproval(
            string deliverableId,
            string status,
            [FromBody] DeliverableFileApprovalRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(deliverableId))
            {
                return BadRequest(new { success = false, message = "DeliverableId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.FileId))
            {
                return BadRequest(new { success = false, message = "FileId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(new { success = false, message = "FileName is required." });
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                return BadRequest(new { success = false, message = "Status is required." });
            }

            var normalizedStatus = status.Trim();
            if (!string.Equals(normalizedStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Status must be Approved or Rejected." });
            }

            var statusValue = char.ToUpper(normalizedStatus[0]) + normalizedStatus[1..].ToLower();

            try
            {
                var record = await _approvalService.UpsertAsync(
                    deliverableId,
                    request.FileId.Trim(),
                    request.FileName.Trim(),
                    request.FileUrl?.Trim(),
                    request.Notes?.Trim(),
                    statusValue);

                try
                {
                    var creativeNotesPayload = BuildCreativeNotesPayload(statusValue, request);
                    var zohoResult = await _zohoService.UpdateDeliverableCreativeNotesAsync(
                        deliverableId,
                        creativeNotesPayload);

                    if (string.IsNullOrWhiteSpace(zohoResult))
                    {
                        return StatusCode(502, new
                        {
                            success = false,
                            step = "zoho",
                            message = "File approval was saved, but Zoho update failed.",
                            data = record
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Zoho update failed for deliverable {DeliverableId}", deliverableId);
                    return StatusCode(502, new
                    {
                        success = false,
                        step = "zoho",
                        message = "File approval was saved, but Zoho update failed.",
                        error = ex.Message,
                        data = record
                    });
                }

                try
                {
                    var emailBody = $"Deliverable : {request.Name}<br/>" +
                                    $"Company Name : {request.Company}<br/>" +
                                    $"File Name : {request.FileName}<br/>" +
                                    $"File Link : {(string.IsNullOrWhiteSpace(request.FileUrl) ? "N/A" : $"<a href='{request.FileUrl}'>{request.FileUrl}</a>")}<br/>" +
                                    $"Deliverable Link : <a href='https://crm.zoho.com/crm/org668627929/tab/CustomModule22/{deliverableId}'>Zoho Deliverable Link</a><br/>" +
                                    $"Client Notes : {request.Notes}<br/>" +
                                    $"Approval Status : {statusValue}";
                    await _emailService.SendClientApprovalAsync(statusValue, emailBody, deliverableId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Approval email failed for deliverable {DeliverableId}", deliverableId);
                    return Ok(new
                    {
                        success = true,
                        warning = "File approval was saved, but the notification email failed to send.",
                        error = ex.Message,
                        data = record
                    });
                }

                return Ok(new { success = true, data = record });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database save failed for deliverable {DeliverableId}", deliverableId);
                return StatusCode(500, new
                {
                    success = false,
                    step = "database",
                    message = "Failed to save file approval to the database.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File approval failed for deliverable {DeliverableId}", deliverableId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred while saving file approval.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{deliverableId}")]
        public async Task<IActionResult> GetFileApprovals(string deliverableId)
        {
            if (string.IsNullOrWhiteSpace(deliverableId))
            {
                return BadRequest(new { success = false, message = "DeliverableId is required." });
            }

            try
            {
                var records = await _approvalService.GetByDeliverableIdAsync(deliverableId);
                return Ok(new { success = true, data = records });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database read failed for deliverable {DeliverableId}", deliverableId);
                return StatusCode(500, new
                {
                    success = false,
                    step = "database",
                    message = "Failed to load file approvals from the database.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load file approvals for deliverable {DeliverableId}", deliverableId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred while loading file approvals.",
                    error = ex.Message
                });
            }
        }

        private static string BuildCreativeNotesPayload(string status, DeliverableFileApprovalRequest request)
        {
            var parts = new List<string>
            {
                $"Status: {status}",
                $"File: {request.FileName}",
                $"FileId: {request.FileId}"
            };

            if (!string.IsNullOrWhiteSpace(request.FileUrl))
            {
                parts.Add($"URL: {request.FileUrl.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                parts.Add($"Notes: {request.Notes.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                parts.Add($"Deliverable: {request.Name.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(request.Company))
            {
                parts.Add($"Company: {request.Company.Trim()}");
            }

            return string.Join(" | ", parts);
        }
    }
}
