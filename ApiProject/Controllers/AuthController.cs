using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;



namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly BlacklistService _blacklistService;
        private readonly JwtService _jwtService;

        public AuthController(AuthService authService, BlacklistService blacklistService, JwtService jwtService)
        {
            _authService = authService;
            _blacklistService = blacklistService;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Request body is missing" });

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "Email and password are required" });

            try
            {
                var result = await _authService.LoginAsync(dto.Email, dto.Password);

                if (result == null)
                    return StatusCode(500, new { success = false, message = "Internal server error while processing login" });

                if (string.IsNullOrEmpty(result.Token))
                    return Unauthorized(new { success = false, message = "Invalid email or password" });

                if (string.IsNullOrEmpty(result.RoleName))
                    return Ok(new { success = true, message = "Login successful but user has no role assigned", data = result });

                var message = result.Permissions.Count == 0
                    ? $"Login successful. You are logged in as '{result.RoleName}', but you have no permissions assigned yet."
                    : $"Login successful. You are logged in as '{result.RoleName}'.";

                return Ok(new
                {
                    success = true,
                    message,
                    data = new
                    {
                        token = result.Token,
                        role = result.RoleName,
                        permissions = result.Permissions
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Unexpected error during login: {ex.Message}" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                    return BadRequest(new { success = false, message = "Authorization header is missing" });

                var token = authHeader.ToString().Replace("Bearer ", "");
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { success = false, message = "Token not found" });

                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                System.IdentityModel.Tokens.Jwt.JwtSecurityToken validatedToken;

                try
                {
                    var principal = tokenHandler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtService.Secret)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false
                    }, out var tmp);

                    validatedToken = tmp as System.IdentityModel.Tokens.Jwt.JwtSecurityToken ?? throw new Microsoft.IdentityModel.Tokens.SecurityTokenException("Invalid token");
                }
                catch
                {
                    return BadRequest(new { success = false, message = "Invalid token or signature" });
                }

                if (await _blacklistService.IsTokenRevokedAsync(token))
                    return NotFound(new { success = false, message = "Token has already been revoked" });

                await _blacklistService.AddToBlacklistAsync(token, validatedToken.ValidTo);

                return Ok(new
                {
                    success = true,
                    message = "Logout successful. Token is now invalidated.",
                    revokedUntil = validatedToken.ValidTo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Unexpected error during logout: {ex.Message}" });
            }
        }
    }
}
