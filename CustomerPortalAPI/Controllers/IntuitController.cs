using Microsoft.AspNetCore.Mvc;
using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using System.Net.Http.Headers;
//using Microsoft.OpenApi.Models;
using RestSharp;
using System.Text;
using System.Text.Json;

namespace CustomerPortalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class IntuitController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly QuickBookServices _quickBookServices;
    private const string TokenUrl = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
    private const string ApiBaseUrl = "https://quickbooks.api.intuit.com/v3/company/";
    private string ClientId = "";
    private string ClientSecret = "";
    private string RedirectUri = "";
    private static string AccessToken;
    private static string RefreshToken;

    public IntuitController(IConfiguration configuration, QuickBookServices quickBookServices)
    {
        _configuration = configuration;
        _quickBookServices = quickBookServices;
        ClientId = _configuration["intuit:ClientId"];
        ClientSecret = _configuration["intuit:ClientSecret"];
        RedirectUri = _configuration["intuit:redirectUrl"];
    }

    // Step 1: Redirect to QuickBooks Authorization URL
    [HttpGet("auth")]
    public IActionResult GetAuthUrl()
    {
        var authUrl = $"https://appcenter.intuit.com/connect/oauth2?client_id={ClientId}&response_type=code&scope=com.intuit.quickbooks.accounting&state=security&redirect_uri={RedirectUri}";
        return Redirect(authUrl);
    }

    // Step 2: Handle Callback and Exchange Code for Tokens
    [HttpGet("callback")]
    public async Task<IActionResult> ExchangeCodeForToken(string code)
    {
        try
        {
            var tokenResponse = await _quickBookServices.ExchangeAuthorizationCode(code);
            Console.WriteLine(tokenResponse);
            return Ok(JsonConvert.DeserializeObject(tokenResponse));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    // Step 3: Refresh Token
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshTokenAsync()
    {
        var data = await _quickBookServices.RefreshToken();
        return Ok(data);
    }

    // Step 4: Fetch Invoices
    [HttpGet("customer/{CustomerName}")]
    public async Task<IActionResult> GetCustomer(string CustomerName)
    {
        var companyId = _configuration["intuit:realmid"];
        var lookup = await _quickBookServices.getCustomerId(companyId, CustomerName);
        if (lookup.Contains("AuthenticationFailed", StringComparison.OrdinalIgnoreCase)
            || lookup.Contains("Reconnect QuickBooks", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(401, new { message = "QuickBooks rejected the saved sign-in. Open Intuit connect and authorize the app again." });
        }
        try
        {
            var document = JsonDocument.Parse(lookup);
            if (!document.RootElement.TryGetProperty("QueryResponse", out var query)
                || !query.TryGetProperty("Customer", out var customers)
                || customers.GetArrayLength() == 0)
            {
                return NotFound(new { message = "No QuickBooks customer matches that company." });
            }

            var match = customers.ValueKind == JsonValueKind.Array
                ? (customers.GetArrayLength() == 0 ? (JsonElement?)null : customers[0])
                : customers.ValueKind == JsonValueKind.Object ? customers : null;
            if (match == null)
            {
                return NotFound(new { message = "No QuickBooks customer matches that company." });
            }

            var customerId = JsonText(match.Value, "Id");
            if (string.IsNullOrEmpty(customerId))
            {
                return StatusCode(502, new { message = "QuickBooks did not return a customer id." });
            }

            var raw = await _quickBookServices.GetCustomer(customerId);
            var customerDocument = JsonDocument.Parse(raw);
            if (!customerDocument.RootElement.TryGetProperty("Customer", out var customer))
            {
                return StatusCode(502, new { message = "QuickBooks did not return the customer.", detail = raw });
            }

            string? email = null;
            if (customer.TryGetProperty("PrimaryEmailAddr", out var emailNode))
            {
                email = JsonText(emailNode, "Address");
            }

            var hasBill = customer.TryGetProperty("BillAddr", out var bill);
            return Ok(new
            {
                id = customerId,
                primaryEmailAddr = new { address = email },
                billAddr = hasBill ? new
                {
                    id = JsonText(bill, "Id"),
                    line1 = JsonText(bill, "Line1"),
                    city = JsonText(bill, "City"),
                    countrySubDivisionCode = JsonText(bill, "CountrySubDivisionCode"),
                    postalCode = JsonText(bill, "PostalCode"),
                    country = JsonText(bill, "Country"),
                    lat = JsonText(bill, "Lat"),
                    @long = JsonText(bill, "Long"),
                } : null,
            });
        }
        catch (Exception)
        {
            return StatusCode(502, lookup);
        }
    }

    [HttpGet("invoices/{CustomerName}")]
    public async Task<IActionResult> GetInvoices(string CustomerName)
    {
        var data = await _quickBookServices.getInvoices(CustomerName);
        return Ok(data);
    }

    [HttpGet("invoice/pdf/{invoiceid}")]
    public async Task<IActionResult> getInvoicePDF(string invoiceid)
    {
        var pdfStream = await _quickBookServices.getInvoicePDF(invoiceid);
        return File(pdfStream, "application/pdf", $"Invoice_{invoiceid}.pdf");
    }

    static string? JsonText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }
}

// Helper class for token response
public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string ExpiresIn { get; set; }
}