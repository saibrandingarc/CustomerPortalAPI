using System;
namespace CustomerPortalAPI.Models
{
	public class ApiResponse
	{
        public bool Status { get; set; }
        public string Message { get; set; }
        public string? Obj { get; set; }
        public string? Zoho { get; set; }
        public bool? EmailVerified { get; set; }
        public bool? OtpVerified { get; set; }

        public ApiResponse(bool status, string message, string obj, string zoho, bool emailVerified, bool otpVerified)
        {
            Status = status;
            Message = message;
            Obj = obj;
            Zoho = zoho;
            EmailVerified = emailVerified;
            OtpVerified = otpVerified;
        }

        public ApiResponse()
        {
        }
    }
}

