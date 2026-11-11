using System;
using Azure.Core;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CustomerPortalAPI.Controllers;
using CustomerPortalAPI.Models;
using Newtonsoft.Json;

namespace CustomerPortalAPI.Services
{
    public class SettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ZohoServices _zohoOAuthService;

        public SettingsService(ILogger<SettingsService> logger,
           ApplicationDbContext context, ZohoServices zohoOAuthService)
        {
            _context = context;
            _zohoOAuthService = zohoOAuthService;
        }

        public async Task<string?> AddSettingsAsync(string zohotoken)
        {
            var accessToken = "";
            using (JsonDocument document = JsonDocument.Parse(zohotoken))
            {
                JsonElement root = document.RootElement;

                accessToken = root.GetProperty("access_token").GetString();
                
                var newSetting = new Settings
                {
                    AccessToken = root.GetProperty("access_token").GetString(),
                    RefreshToken = root.GetProperty("refresh_token").GetString(),
                    Scope = root.GetProperty("scope").GetString(),
                    ExpiresIn = root.GetProperty("expires_in").GetInt32(),
                    DateAdded = DateTime.UtcNow
                };

                _context.Settings.Add(newSetting);
                await _context.SaveChangesAsync();
                return accessToken;
            }
            
        }

        //public async Task<bool> UpdateSettingsAsync(int id, string newToken)
        //{
        //    // Retrieve the entity you want to update
        //    var settings = await _context.Settings.FindAsync(id);

        //    // If the entity is not found, return false
        //    if (settings == null)
        //    {
        //        return false;
        //    }

        //    // Make changes to the entity
        //    settings.zohotoken = newToken;
        //    settings.DateAdded = DateTime.UtcNow;

        //    // Save changes back to the database
        //    await _context.SaveChangesAsync();

        //    return true;
        //}
    }

}

