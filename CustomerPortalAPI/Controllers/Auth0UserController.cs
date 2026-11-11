using System;
using Microsoft.AspNetCore.Mvc;
using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.ComponentModel.Design;

namespace CustomerPortalAPI.Controllers
{
	public class Auth0UserController : ControllerBase
    {
        private readonly Auth0Services _auth0UserService;
        private readonly UserService _userService;
        private readonly ZohoServices _zohoOAuthService;

        public Auth0UserController(Auth0Services auth0UserService, UserService userService, ZohoServices zohoOAuthService)
        {
            _auth0UserService = auth0UserService;
            _userService = userService;
            _zohoOAuthService = zohoOAuthService;
        }

        [HttpPost("create-auth0-user")]
        public async Task<IActionResult> CreateAuth0User([FromBody] User user)
        {
            var response = new ApiResponse();
            try
            {
                //var zohoAccountDetails = await _zohoOAuthService.checkEmail(user.Email);
                //if (zohoAccountDetails == "") {
                //    response = new ApiResponse(true, "Email is not valid, please contact your admin.", "", "", false, false);
                //} else {
                //    var result = await _auth0UserService.checkEmailInAuth0(user.Email);
                //    if (!result)
                //    {
                //        JObject jsonObj = JObject.Parse(zohoAccountDetails);
                //        user.first_name = jsonObj["data"][0]["First_Name"].ToString();
                //        user.full_name = jsonObj["data"][0]["Full_Name"].ToString();
                        var userStatus = await _auth0UserService.createUserInAuth0Admin(user);
                        JObject userAuthStatus = JObject.Parse(userStatus);
                        var roleAssignStatus = await _auth0UserService.assignRoleTOUserInAuth0Admin(userAuthStatus["user_id"].ToString());
                //        user.Auth0Status = true;
                //        user.Auth0AccountId = userAuthStatus["user_id"].ToString();
                //        var res = await _userService.SaveUser(user);
                //        if (res == 201)
                //        {
                //            response = new ApiResponse(true, "User created", "", "", true, true);
                //        }
                //        else
                //        {
                //            response = new ApiResponse(true, "Email already registered", "", "", true, true);
                //        }

                //    } else {
                //        response = new ApiResponse(true, "Email already registered", "", "", true, true);
                //    }
                //}
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
            return Ok(response);
        }

        [HttpDelete("auth0/deleteUser")]
        public async Task<IActionResult> DeleteUserAsync([FromBody] User user)
        {
            try
            {
                string id = user.Auth0AccountId.Split('|')[1];
                var res = await _auth0UserService.DeleteUserAsync(id);
                if(res == "201")
                {
                    var response = await _userService.DeleteUser(user.Id);
                    if (response == 201)
                    {
                        return Ok(response);
                    }
                    else
                    {
                        return BadRequest(new { status = "error", message = "User not exist" });
                    }
                } else
                {
                    return Ok(res);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("auth0/users")]
        public async Task<IActionResult> GetUsers()
        {
            var cases = await _userService.GetUsers();
            return Ok(cases);
        }
    }
}

