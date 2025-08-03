using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;

namespace OtakuNest.Gateway.Configurations
{
    public class CustomRateLimitConfiguration : RateLimitConfiguration
    {
        public CustomRateLimitConfiguration(
            IOptions<IpRateLimitOptions> ipOptions,
            IOptions<ClientRateLimitOptions> clientOptions)
            : base(ipOptions, clientOptions)
        {
        }
    }
}
