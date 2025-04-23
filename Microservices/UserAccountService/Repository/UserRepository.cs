using AccountService.Database.Data;
using AccountService.Models;
using Microsoft.EntityFrameworkCore;

namespace UserAccountService.Repository;

public class UserRepository(UserAccountDbContext context) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }
}
