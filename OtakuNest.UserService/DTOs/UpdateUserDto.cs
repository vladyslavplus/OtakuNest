﻿namespace OtakuNest.UserService.DTOs
{
    public class UpdateUserDto
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
