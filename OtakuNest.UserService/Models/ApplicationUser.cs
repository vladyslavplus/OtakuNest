﻿using Microsoft.AspNetCore.Identity;

namespace OtakuNest.UserService.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime CreatedAt {  get; set; } = DateTime.UtcNow;
    }
}
