using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ClosedXML.Excel;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly ILogger<StudentsController> _logger;
        private readonly IWebHostEnvironment _env;

        // allowed image extensions and size limit
        private static readonly string[] AllowedImageExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        private const long MaxImageBytes = 20 * 1024 * 1024; // 20MB

        public StudentsController(StudentService studentService, ILogger<StudentsController> logger, IWebHostEnvironment env)
        {
            _studentService = studentService;
            _logger = logger;
            _env = env;
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? name, [FromQuery] string? email, [FromQuery] List<int>? courseIds, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var (students, total) = await _studentService.GetStudentsPagedAsync(name, email, courseIds, page, pageSize);

            if (students == null || !students.Any())
                return NotFound(new { success = false, message = "No students found with the given filters", count = 0, total = 0 });

            return Ok(new { success = true, count = students.Count, total, page, pageSize, data = students });
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var student = await _studentService.GetByIdAsync(id);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {id} not found" });

            var dto = new StudentDto
            {
                Id = student.Id,
                Name = student.Name,
                Email = student.Email,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt,
                PhotoPath = student.PhotoPath,
                Courses = student.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList() ?? new List<CourseDto>()
            };

            return Ok(new { success = true, data = dto });
        }

        [Authorize(Roles = "Admin,Teacher,Student")]
        [HttpGet("{id}/photo")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var student = await _studentService.GetByIdAsync(id);
            if (student == null) return NotFound(new { success = false, message = $"Student {id} not found" });

            // if image stored in DB
            if (student.PhotoData != null && !string.IsNullOrEmpty(student.PhotoContentType))
                return File(student.PhotoData, student.PhotoContentType);

            // if image stored on disk
            if (!string.IsNullOrEmpty(student.PhotoPath) && System.IO.File.Exists(student.PhotoPath))
            {
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(student.PhotoPath, out var contentType))
                    contentType = "application/octet-stream";

                try
                {
                    var fs = System.IO.File.OpenRead(student.PhotoPath);
                    return File(fs, contentType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read photo file for student {StudentId}", id);
                    return StatusCode(500, new { success = false, message = "Error reading photo file" });
                }
            }

            return NotFound(new { success = false, message = "Photo not found" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] StudentCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            var student = new Student
            {
                Name = dto.Name,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow
            };

            // التعامل مع الصورة
            if (dto.Photo != null)
            {
                // تحقق من الامتداد
                var ext = Path.GetExtension(dto.Photo.FileName).ToLowerInvariant();
                if (!AllowedImageExt.Contains(ext))
                    return BadRequest(new { success = false, message = "Unsupported image extension." });

                if (dto.Photo.Length > MaxImageBytes)
                    return BadRequest(new { success = false, message = $"Image too large (max {MaxImageBytes} bytes)." });

                if (dto.SaveInDatabase)
                {
                    using var ms = new MemoryStream();
                    await dto.Photo.CopyToAsync(ms);
                    student.PhotoData = ms.ToArray();
                    student.PhotoContentType = dto.Photo.ContentType ?? "application/octet-stream";
                    student.PhotoPath = null; // مسح المسار لو خزنت بالقاعدة
                }
                else
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var folder = Path.Combine(webRoot, "images");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(folder, fileName);

                    try
                    {
                        using var stream = new FileStream(filePath, FileMode.CreateNew);
                        await dto.Photo.CopyToAsync(stream);
                        student.PhotoPath = filePath;
                        student.PhotoData = null; // مسح البيانات لو خزنت كملف
                        student.PhotoContentType = null;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Failed to save uploaded image");
                        return StatusCode(500, new { success = false, message = "Failed to save uploaded image" });
                    }
                }
            }
            else
            {
                // لم تُرسل صورة → كلها null
                student.PhotoPath = null;
                student.PhotoData = null;
                student.PhotoContentType = null;
            }

            try
            {
                await _studentService.AddAsync(student, dto.CourseIds);
                return CreatedAtAction(nameof(GetById), new { id = student.Id },
                    new { success = true, id = student.Id, message = "Student created successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Create failed");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating student");
                return StatusCode(500, new { success = false, message = "Unexpected server error" });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] StudentUpdateDto dto)
        {
            var student = await _studentService.GetByIdAsync(id);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {id} not found" });

            student.Name = dto.Name;
            student.Email = dto.Email;
            student.UpdatedAt = DateTime.UtcNow;

            string? newPhotoPath = null;
            byte[]? newPhotoData = null;
            string? newPhotoContentType = null;

            if (dto.Photo != null)
            {
                var ext = Path.GetExtension(dto.Photo.FileName).ToLowerInvariant();
                if (!AllowedImageExt.Contains(ext))
                    return BadRequest(new { success = false, message = "Unsupported image extension." });
                if (dto.Photo.Length > MaxImageBytes)
                    return BadRequest(new { success = false, message = $"Image too large (max {MaxImageBytes} bytes)." });

                if (dto.SaveInDatabase)
                {
                    using var ms = new MemoryStream();
                    await dto.Photo.CopyToAsync(ms);
                    newPhotoData = ms.ToArray();
                    newPhotoContentType = dto.Photo.ContentType ?? "application/octet-stream";
                }
                else
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var folder = Path.Combine(webRoot, "images");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(folder, fileName);

                    try
                    {
                        using var stream = new FileStream(filePath, FileMode.CreateNew);
                        await dto.Photo.CopyToAsync(stream);
                        newPhotoPath = filePath;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Failed to save uploaded image");
                        return StatusCode(500, new { success = false, message = "Failed to save uploaded image" });
                    }
                }
            }

            try
            {
                await _studentService.UpdateAsync(student, dto.CourseIds, newPhotoPath, newPhotoData, newPhotoContentType, dto.ForceReplacePhoto);
                return Ok(new { success = true, message = $"Student {id} updated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Update failed");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating student");
                return StatusCode(500, new { success = false, message = "Unexpected server error" });
            }
        }


        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _studentService.DeleteAsync(id);
                return Ok(new { success = true, message = $"Student {id} deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {StudentId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected server error" });
            }
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpDelete("{id}/photo")]
        public async Task<IActionResult> DeletePhoto(int id)
        {
            try
            {
                await _studentService.DeletePhotoAsync(id);
                return Ok(new { success = true, message = $"Photo for student {id} deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting photo for student {StudentId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected server error" });
            }
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] int? studentId, [FromQuery] List<int>? courseIds)
        {
            var students = await _studentService.GetStudentsForExportAsync(studentId, courseIds);
            if (!students.Any())
                return NotFound(new { success = false, message = "No students found to export" });

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Students");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "Courses";

            for (int i = 0; i < students.Count; i++)
            {
                var s = students[i];
                ws.Cell(i + 2, 1).Value = s.Id;
                ws.Cell(i + 2, 2).Value = s.Name;
                ws.Cell(i + 2, 3).Value = s.Email;
                ws.Cell(i + 2, 4).Value = s.CourseTitles != null && s.CourseTitles.Any()
                    ? string.Join(", ", s.CourseTitles)
                    : "";
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Students.xlsx");
        }
    }
}
