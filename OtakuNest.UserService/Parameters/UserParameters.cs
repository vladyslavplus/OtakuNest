using OtakuNest.Common.Parameters;

namespace OtakuNest.UserService.Parameters
{
    public class UserParameters : QueryStringParameters
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? CreatedAtFrom { get; set; }
        public DateTime? CreatedAtTo { get; set; }
        public bool? EmailConfirmed { get; set; }
        public bool? PhoneNumberConfirmed { get; set; }
    }
}
