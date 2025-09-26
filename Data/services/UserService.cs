using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Data.Services
{
    public class UserService
    {
        private readonly BankDbContext _context;

        public UserService(BankDbContext context)
        {
            _context = context;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            var enteredHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(enteredPassword))
            );
            return enteredHash == storedHash;
        }

        // إضافة مستخدم جديد
        public async Task<User> AddUserAsync(string username, string email, string password, int roleId = 3)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("Username is required");
            if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Email is required");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6) throw new InvalidOperationException("Password must be at least 6 characters");
            if (roleId < 1 || roleId > 3) throw new InvalidOperationException("RoleId must be between 1 and 3");

            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new InvalidOperationException("Email already exists");

            var user = new User
            {
                Username = username,
                Email = email,
                RoleId = roleId,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        // تحديث بيانات المستخدم + باسورد اختياري
        public async Task<bool> UpdateUserAsync(User user, string? newPassword = null)
        {
            var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing == null) return false;

            if (await _context.Users.AnyAsync(u => u.Email == user.Email && u.Id != user.Id))
                throw new InvalidOperationException("Email already in use by another user");

            existing.Username = user.Username;
            existing.Email = user.Email;

            if (!string.IsNullOrEmpty(newPassword))
                existing.PasswordHash = HashPassword(newPassword);

            await _context.SaveChangesAsync();
            return true;
        }

        // حذف مستخدم
        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        // استعلامات
        public async Task<List<User>> GetAllAsync() => await _context.Users.OrderByDescending(u => u.Id).ToListAsync();
        public async Task<User?> GetByIdAsync(int id) => await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        public async Task<User?> GetByEmailAsync(string email) => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<Role?> GetRoleByIdAsync(int roleId) =>
            await _context.Roles.FindAsync(roleId);

        public async Task<List<User>> SearchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<User>();
            return await _context.Users
                .Where(u => EF.Functions.Like(u.Username, $"%{name}%"))
                .OrderByDescending(u => u.Id)
                .ToListAsync();
        }
    }
}
