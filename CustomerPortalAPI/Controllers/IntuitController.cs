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

}

// Helper class for token response
public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string ExpiresIn { get; set; }
}