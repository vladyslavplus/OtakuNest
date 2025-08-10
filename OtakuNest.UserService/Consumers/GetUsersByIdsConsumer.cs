using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Contracts;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Consumers
{
    public class GetUsersByIdsConsumer : IConsumer<GetUsersByIdsRequest>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public GetUsersByIdsConsumer(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task Consume(ConsumeContext<GetUsersByIdsRequest> context)
        {
            var ids = context.Message.UserIds.ToList();

            var users = await _userManager.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new UserShortInfo(u.Id, u.UserName!))
                .ToListAsync(); 

            await context.RespondAsync(new GetUsersByIdsResponse(users));
        }
    }
}
