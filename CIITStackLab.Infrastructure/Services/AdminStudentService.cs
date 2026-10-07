using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminStudentService : IAdminStudentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminStudentService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<AdminStudentPageDto> GetPageAsync(
        CancellationToken cancellationToken = default)
    {
        var studentRoleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(studentRoleId))
        {
            return new AdminStudentPageDto();
        }

        var students = await (
            from userRole in _dbContext.UserRoles.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on userRole.UserId equals user.Id
            where userRole.RoleId == studentRoleId
            orderby user.UserName
            select new AdminStudentDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed
            })
            .ToListAsync(cancellationToken);

        return new AdminStudentPageDto
        {
            ActiveStudents = students.Where(x => x.IsActive).ToList(),
            InactiveStudents = students.Where(x => !x.IsActive).ToList()
        };
    }

    public async Task<(bool Succeeded, string? Error)> SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var studentRoleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(studentRoleId))
        {
            return (false, "The Student role does not exist in the current ERP Identity database.");
        }

        var isStudent = await _dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.RoleId == studentRoleId,
                cancellationToken);

        if (!isStudent)
        {
            return (false, "The selected account is not a Student account.");
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return (false, "The selected student account was not found.");
        }

        if (user.IsActive == isActive)
        {
            return (true, null);
        }

        user.IsActive = isActive;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return (false, "The student account status could not be updated.");
        }

        return (true, null);
    }
}
