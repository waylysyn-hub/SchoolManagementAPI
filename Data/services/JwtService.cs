using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Data.Services
{
    public class JwtService
    {
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtService(string secret, string issuer, string audience)
        {
            _secret = secret;
            _issuer = issuer;
            _audience = audience;
        }

        public string Secret => _secret; // للوصول من الخارج (مثلاً للـ logout)

        public string GenerateToken(User user, List<string> permissions)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),

                // 🔹 role_id & role_name لتستخدمها انت بالـ policies
                new Claim("role_id", user.RoleId.ToString()),
                new Claim("role_name", user.Role?.Name ?? "User"),

                // 🔹 ClaimTypes.Role ليخلي [Authorize(Roles="Admin")] يشتغل
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "User")
            };

            // ✅ أضف كل Permission كـ Claim مستقل
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
