using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionsController : ControllerBase
    {
        private readonly PermissionService _permissionService;

        public PermissionsController(PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        // =========================
        // GET /permissions
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _permissionService.GetAllPermissionsAsync();
                return Ok(new
                {
                    success = true,
                    data = permissions.Select(p => new { p.Id, p.Name })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unexpected error while fetching all permissions",
                    details = ex.Message
                });
            }
        }

        // =========================
        // GET /permissions/user/{userId}
        // =========================
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var permissions = await _permissionService.GetUserPermissionsAsync(userId);
                return Ok(new
                {
                    success = true,
                    data = permissions.Select(p => new { p.Id, p.Name })
                });
            }
            catch (KeyNotFoundException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Unexpected error while fetching permissions for user {userId}",
                    details = ex.Message
                });
            }
        }

        // =========================
        // POST /permissions/user/{userId}/add/{permissionId}
        // =========================
        [HttpPost("user/{userId}/add/{permissionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPermissionToUser(int userId, int permissionId)
        {
            try
            {
                var message = await _permissionService.AddPermissionToUserAsync(userId, permissionId);
                if (message.Contains("not found"))
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Unexpected error while adding permission {permissionId} to user {userId}",
                    details = ex.Message
                });
            }
        }

        // =========================
        // DELETE /permissions/user/{userId}/remove/{permissionId}
        // =========================
        [HttpDelete("user/{userId}/remove/{permissionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePermissionFromUser(int userId, int permissionId)
        {
            try
            {
                var message = await _permissionService.RemovePermissionFromUserAsync(userId, permissionId);
                if (message.Contains("not found"))
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Unexpected error while removing permission {permissionId} from user {userId}",
                    details = ex.Message
                });
            }
        }
    }
}
