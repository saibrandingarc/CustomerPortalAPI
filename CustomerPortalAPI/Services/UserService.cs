using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using CustomerPortalAPI.Models;

namespace CustomerPortalAPI.Services
{
	public class UserService
	{
        private readonly ApplicationDbContext _context;
        private readonly Auth0Services _auth0Service;

        public UserService(ApplicationDbContext context, Auth0Services auth0Services)
        {
            _context = context;
            _auth0Service = auth0Services;
        }

        public async Task<ActionResult<IEnumerable<User>>?> GetUsers()
        {
            var userData = await _context.Users.ToListAsync();

            // If user not found, return false
            if (userData == null)
            {
                return null;
            }

            // Return the verification status
            return userData;
        }

        public async Task<User> IsEmailVerifiedAsync(string email)
        {
            var userData = await _context.Users
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync();

            // If user not found, return false
            if (userData == null)
            {
                return null;
            }

            // Return the verification status
            return userData;
        }

        public async Task SaveEmailAndOtp(string email, string otp, string companyname, string companyId, string fullname)
        {
            var userData = await _context.Users
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync();

            if (userData == null)
            {
                var user = new User
                {
                    Email = email,
                    Otp = otp,
                    CompanyName = companyname,
                    CompanyId = companyId,
                    ZohoEmailStatus = true,
                    OTPStatus = false,
                    Auth0Status = false,
                    full_name = fullname
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            } else
            {
                userData.Otp = otp;
                _context.Users.Update(userData);
                await _context.SaveChangesAsync();
            }

            
        }

        public async Task<User> updateUser(string email, string password, JObject userAuthStatus)
        {
            var userData = await _context.Users
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync();

            userData.Auth0Status = true;
            userData.Auth0AccountId = userAuthStatus["user_id"].ToString();
            userData.Password = password;
            _context.Users.Update(userData);
            var saveStatus = await _context.SaveChangesAsync();
            return userData;
        }

        public async Task<User> verifyOTP(string email, string otp, string password)
        {
            var user = await _context.Users
            .Where(u => u.Email == email && u.Otp == otp)
            .FirstOrDefaultAsync();

            // If user not found, return false
            if (user == null)
            {
                return null;
            } else
            {
                user.OTPStatus = true;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            // Return the verification status
            return user;
        }

        public async Task<User> login(string email, string password)
        {
            var user = await _context.Users
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync();

            // If user not found, return false
            if (user.Auth0Status)
            {
                // Auth0 login and get token
                var result = await _auth0Service.AuthenticateUserAsync(email, password);
                Console.WriteLine(result);
            }
            else
            {
                Console.WriteLine("Auth0 not exist");
            }

            // Return the verification status
            return user;
        }

        public async Task<int> SaveUser(User user)
        {
            var userData = await _context.Users
            .Where(u => u.Email == user.Email)
            .FirstOrDefaultAsync();

            if (userData == null)
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return 201;
            }
            else
            {
                return 303;
            }
        }

        public async Task<int> DeleteUser(int id)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (existingUser == null)
                return 404;

            _context.Users.Remove(existingUser);
            var res = await _context.SaveChangesAsync();
            return res;
        }

        internal Task UpdatePassword(string email, string password)
        {
            throw new NotImplementedException();
        }

        public async Task RecordLoginActivityAsync(
            string email,
            string activityType,
            string status,
            string? details = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            var activity = new LoginActivity
            {
                Email = email,
                ActivityType = activityType,
                Status = status,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.LoginActivities.Add(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetRecentFailedLoginCountAsync(string email, int withinMinutes)
        {
            var fromUtc = DateTime.UtcNow.AddMinutes(-withinMinutes);

            return await _context.LoginActivities
                .Where(a => a.Email == email
                    && a.Status == "Failed"
                    && a.CreatedAtUtc >= fromUtc)
                .CountAsync();
        }

        public async Task<bool> HasRecentFailureAlertAsync(string email, int withinMinutes)
        {
            var fromUtc = DateTime.UtcNow.AddMinutes(-withinMinutes);

            return await _context.LoginActivities
                .Where(a => a.Email == email
                    && a.ActivityType == "FailedLoginAlert"
                    && a.Status == "Notified"
                    && a.CreatedAtUtc >= fromUtc)
                .AnyAsync();
        }
    }
}

