using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly PermissionService _permissionService;

        public UsersController(UserService userService, PermissionService permissionService)
        {
            _userService = userService;
            _permissionService = permissionService;
        }

        // ===========================
        // Get All Users
        // ===========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            if (!users.Any())
                return Ok(new { success = true, message = "No users found", data = new object[0] });

            return Ok(new
            {
                success = true,
                count = users.Count,
                data = users.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.RoleId,
                    u.CreatedAt
                })
            });
        }

        // ===========================
        // Get User by Id
        // ===========================
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            var permissions = await _permissionService.GetUserPermissionsAsync(id);

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.RoleId,
                    user.CreatedAt,
                    Permissions = permissions.Select(p => new { p.Id, p.Name })
                }
            });
        }

        // ===========================
        // Register User
        // ===========================
        [HttpPost("register")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data. Please check the fields.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { success = false, message = "Password and Confirm Password do not match." });

            var user = await _userService.AddUserAsync(dto.Username, dto.Email, dto.Password, dto.RoleId);

            var rolePermissions = await _permissionService.GetPermissionsByRoleIdAsync(dto.RoleId);
            foreach (var perm in rolePermissions)
                await _permissionService.AddPermissionToUserAsync(user.Id, perm.Id);

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
            {
                success = true,
                message = "User registered successfully.",
                data = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.RoleId,
                    Permissions = rolePermissions.Select(p => new { p.Id, p.Name })
                }
            });
        }

        // ===========================
        // Update Password
        // ===========================
        [HttpPut("{id}/password")]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> UpdatePassword(int id, [FromBody] UpdatePasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            // التحقق من الصلاحيات: فقط Admin أو صاحب الحساب نفسه
            var currentUserEmail = User.Identity?.Name;
            if (currentUserEmail != user.Email && !User.IsInRole("Admin"))
                return Forbid("You can only change your own password.");

            if (!_userService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return Unauthorized(new { success = false, message = "Current password is incorrect." });

            var updated = await _userService.UpdateUserAsync(user, dto.NewPassword);
            if (!updated)
                return BadRequest(new { success = false, message = "Password update failed." });

            return Ok(new { success = true, message = "Password updated successfully." });
        }

        // ===========================
        // Update User Info
        // ===========================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data. Please check the fields.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            var existingUser = await _userService.GetByEmailAsync(dto.Email);
            if (existingUser != null && existingUser.Id != id)
                return Conflict(new { success = false, message = $"Email '{dto.Email}' is already used by another user." });

            var user = new User { Id = id, Username = dto.Username, Email = dto.Email };
            var updated = await _userService.UpdateUserAsync(user);
            if (!updated)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            return Ok(new { success = true, message = "User updated successfully." });
        }

        // ===========================
        // Update User Role
        // ===========================
        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] int newRoleId)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            if (newRoleId < 1 || newRoleId > 3)
                return BadRequest(new { success = false, message = "RoleId must be 1 (Admin), 2 (Teacher), or 3 (Student)." });

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            user.RoleId = newRoleId;
            await _userService.UpdateUserAsync(user);

            var newRolePermissions = await _permissionService.GetPermissionsByRoleIdAsync(newRoleId);
            var currentPermissions = await _permissionService.GetUserPermissionsAsync(id);
            foreach (var perm in currentPermissions)
                await _permissionService.RemovePermissionFromUserAsync(id, perm.Id);
            foreach (var perm in newRolePermissions)
                await _permissionService.AddPermissionToUserAsync(id, perm.Id);

            // جلب اسم الرول الصحيح
            var role = await _userService.GetRoleByIdAsync(newRoleId);

            return Ok(new
            {
                success = true,
                message = $"User role updated to {newRoleId} successfully.",
                RoleId = newRoleId,
                RoleName = role?.Name ?? "Unknown",
                Permissions = newRolePermissions.Select(p => new { p.Id, p.Name })
            });
        }

        // ===========================
        // Delete User
        // ===========================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            var deleted = await _userService.DeleteUserAsync(id);
            if (!deleted)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            return Ok(new { success = true, message = "User deleted successfully." });
        }
    }
}
