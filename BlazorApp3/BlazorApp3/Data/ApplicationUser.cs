using Microsoft.AspNetCore.Identity;

namespace BlazorApp3.Data
{
    public class ApplicationUser : IdentityUser
    {
        // Add any additional user properties here if needed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    }
}