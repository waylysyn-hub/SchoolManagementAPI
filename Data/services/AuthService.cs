using Domain.Entities;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Services
{
    public class AuthService
    {
        private readonly BankDbContext _context;
        private readonly JwtService _jwtService;

        public AuthService(BankDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        private static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            using var sha256 = SHA256.Create();
            var computedHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return computedHash == hash;
        }

        public async Task<LoginResultDto?> LoginAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                var user = await _context.Users
                    .Include(u => u.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                    .Include(u => u.Permissions)
                    .Include(u => u.DeniedPermissions)
                        .ThenInclude(dp => dp.Permission)
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                if (!VerifyPassword(password, user.PasswordHash))
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                var roleName = user.Role?.Name ?? "No role assigned";

                var rolePermissions = user.Role?.RolePermissions.Select(rp => rp.Permission).ToList() ?? new List<Permission>();
                var userPermissions = user.Permissions?.ToList() ?? new List<Permission>();
                var deniedPermissions = user.DeniedPermissions?.Select(dp => dp.Permission).ToList() ?? new List<Permission>();

                var finalPermissions = rolePermissions
                    .Union(userPermissions)
                    .Where(p => !deniedPermissions.Any(dp => dp.Id == p.Id))
                    .Select(p => p.Name)
                    .Distinct()
                    .ToList();

                var token = _jwtService.GenerateToken(user, finalPermissions);

                if (string.IsNullOrEmpty(token))
                    return new LoginResultDto { Token = null!, RoleName = roleName, Permissions = finalPermissions };

                return new LoginResultDto
                {
                    Token = token,
                    RoleName = roleName,
                    Permissions = finalPermissions
                };
            }
            catch (Exception)
            {
                return null; // Controller رح يرجع رسالة مناسبة
            }
        }
    }
}
