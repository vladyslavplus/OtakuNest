using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Parameters;

namespace OtakuNest.UserService.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISortHelper<ApplicationUser> _sortHelper;
        private readonly bool _isInMemory;

        public UserService(UserManager<ApplicationUser> userManager, ISortHelper<ApplicationUser> sortHelper, bool isInMemory = false)
        {
            _userManager = userManager;
            _sortHelper = sortHelper;
            _isInMemory = isInMemory;
        }

        public async Task<PagedList<ApplicationUser>> GetUsersAsync(UserParameters parameters, CancellationToken cancellationToken = default)
        {
            IQueryable<ApplicationUser> query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.UserName))
            {
                if (_isInMemory)
                    query = query.Where(u => u.UserName != null && u.UserName.Contains(parameters.UserName, StringComparison.OrdinalIgnoreCase));
                else
                    query = query.Where(u => EF.Functions.ILike(u.UserName!, $"%{parameters.UserName}%"));
            }

            if (!string.IsNullOrWhiteSpace(parameters.Email))
            {
                if (_isInMemory)
                    query = query.Where(u => u.Email != null && u.Email.Contains(parameters.Email, StringComparison.OrdinalIgnoreCase));
                else
                    query = query.Where(u => EF.Functions.ILike(u.Email!, $"%{parameters.Email}%"));
            }

            if (!string.IsNullOrWhiteSpace(parameters.PhoneNumber))
            {
                if (_isInMemory)
                    query = query.Where(u => u.PhoneNumber != null && u.PhoneNumber.Contains(parameters.PhoneNumber, StringComparison.OrdinalIgnoreCase));
                else
                    query = query.Where(u => u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, $"%{parameters.PhoneNumber}%"));
            }

            if (parameters.CreatedAtFrom.HasValue)
                query = query.Where(u => u.CreatedAt >= parameters.CreatedAtFrom.Value);

            if (parameters.CreatedAtTo.HasValue)
                query = query.Where(u => u.CreatedAt <= parameters.CreatedAtTo.Value);

            if (parameters.EmailConfirmed.HasValue)
                query = query.Where(u => u.EmailConfirmed == parameters.EmailConfirmed.Value);

            if (parameters.PhoneNumberConfirmed.HasValue)
                query = query.Where(u => u.PhoneNumberConfirmed == parameters.PhoneNumberConfirmed.Value);

            query = _sortHelper.ApplySort(query, parameters.OrderBy);

            return await PagedList<ApplicationUser>.ToPagedListAsync(
                query,
                parameters.PageNumber,
                parameters.PageSize,
                cancellationToken);
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
                return false;

            if (!string.IsNullOrWhiteSpace(dto.UserName))
                user.UserName = dto.UserName;

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                user.Email = dto.Email;
                user.EmailConfirmed = false; 
            }

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return false;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, dto.Password);
                if (!passwordResult.Succeeded)
                    return false;
            }

            return true;
        }
    }
}
