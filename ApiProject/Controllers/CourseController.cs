using ClosedXML.Excel;
using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly CourseService _service;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(CourseService service, ILogger<CoursesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // =========================
        // Excel Generator (helper)
        // =========================
        private static MemoryStream GenerateExcel(List<Course> courses)
        {
            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Courses");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Title";
            ws.Cell(1, 3).Value = "Description";
            ws.Cell(1, 4).Value = "TeacherId";
            ws.Cell(1, 5).Value = "Students";

            for (int i = 0; i < courses.Count; i++)
            {
                var c = courses[i];
                ws.Cell(i + 2, 1).Value = c.Id;
                ws.Cell(i + 2, 2).Value = c.Title;
                ws.Cell(i + 2, 3).Value = c.Description ?? "";
                ws.Cell(i + 2, 4).Value = c.TeacherId;
                var studentNames = c.Students?.Select(s => s.Name) ?? Enumerable.Empty<string>();
                ws.Cell(i + 2, 5).Value = string.Join(", ", studentNames);
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        // =========================
        // GET /courses 🔒 قراءة
        // =========================
        [Authorize(Policy = "course.read")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? title, [FromQuery] int? teacherId)
        {
            try
            {
                var courses = (await _service.GetAllAsync()).ToList();

                if (!string.IsNullOrWhiteSpace(title))
                    courses = courses.Where(c => c.Title != null && c.Title.Contains(title, StringComparison.OrdinalIgnoreCase)).ToList();

                if (teacherId.HasValue)
                    courses = courses.Where(c => c.TeacherId == teacherId.Value).ToList();

                if (!courses.Any())
                    return NotFound(new { success = false, message = "No courses found with the given filters" });

                var data = courses.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList();

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAll courses");
                return StatusCode(500, new { success = false, message = "Unexpected error while fetching courses", details = ex.Message });
            }
        }

        // =========================
        // GET /courses/{id} 🔒 قراءة
        // =========================
        [Authorize(Policy = "course.read")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid course ID" });

            try
            {
                var course = await _service.GetByIdAsync(id);
                if (course == null)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                var dto = new CourseDetailDto
                {
                    Id = course.Id,
                    Title = course.Title,
                    Description = course.Description,
                    TeacherId = course.TeacherId,
                    Students = course.Students?.Select(s => new StudentDto
                    {
                        Id = s.Id,
                        Name = s.Name
                    }).ToList() ?? new List<StudentDto>()
                };

                return Ok(new { success = true, data = dto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetById for course {CourseId}", id);
                return StatusCode(500, new { success = false, message = $"Unexpected error while fetching course {id}", details = ex.Message });
            }
        }


        // =========================
        // POST /courses 🔒 إنشاء
        // =========================
        [Authorize(Policy = "course.create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CourseCreateDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Request body is missing" });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { success = false, message = "Course title is required" });

            if (dto.TeacherId <= 0)
                return BadRequest(new { success = false, message = "Invalid teacher ID" });

            try
            {
                var created = await _service.AddAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, new
                {
                    success = true,
                    message = "Course created successfully",
                    id = created.Id
                });
            }
            catch (InvalidOperationException invEx)
            {
                // expected validation-like errors from service (teacher missing, duplicate, missing students...)
                _logger.LogWarning(invEx, "Validation error while creating course");
                return BadRequest(new { success = false, message = invEx.Message });
            }
            catch (ArgumentNullException argEx)
            {
                _logger.LogWarning(argEx, "Invalid input while creating course");
                return BadRequest(new { success = false, message = argEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating course");
                return StatusCode(500, new { success = false, message = "Unexpected error while creating course", details = ex.Message });
            }
        }

        // =========================
        // PUT /courses/{id} 🔒 تعديل
        // =========================
        [Authorize(Policy = "course.update")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CourseUpdateDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid course ID" });

            if (dto == null)
                return BadRequest(new { success = false, message = "Request body is missing" });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { success = false, message = "Course title is required" });

            if (dto.TeacherId <= 0)
                return BadRequest(new { success = false, message = "Invalid teacher ID" });

            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                if (!updated)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                return Ok(new { success = true, message = $"Course {id} updated successfully" });
            }
            catch (InvalidOperationException invEx)
            {
                _logger.LogWarning(invEx, "Validation error while updating course {CourseId}", id);
                return BadRequest(new { success = false, message = invEx.Message });
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid input while updating course {CourseId}", id);
                return BadRequest(new { success = false, message = argEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating course {CourseId}", id);
                return StatusCode(500, new { success = false, message = $"Unexpected error while updating course {id}", details = ex.Message });
            }
        }

        // =========================
        // DELETE /courses/{id} 🔒 حذف
        // =========================
        [Authorize(Policy = "course.delete")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid course ID" });

            try
            {
                var deleted = await _service.DeleteAsync(id);
                if (!deleted)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                return Ok(new { success = true, message = $"Course {id} deleted successfully" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DB error while deleting course {CourseId}", id);
                // foreign key constraint or cascade issue
                return Conflict(new { success = false, message = $"Unable to delete course {id} due to related data or database constraint", details = dbEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting course {CourseId}", id);
                return StatusCode(500, new { success = false, message = $"Unexpected error while deleting course {id}", details = ex.Message });
            }
        }

        // =========================
        // GET /courses/export 🔒 قراءة
        // =========================
        [Authorize(Policy = "course.read")]
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string? title, [FromQuery] int? teacherId)
        {
            try
            {
                var courses = (await _service.GetAllAsync()).ToList();

                if (!string.IsNullOrWhiteSpace(title))
                    courses = courses.Where(c => c.Title != null && c.Title.Contains(title, StringComparison.OrdinalIgnoreCase)).ToList();

                if (teacherId.HasValue)
                    courses = courses.Where(c => c.TeacherId == teacherId.Value).ToList();

                if (!courses.Any())
                    return NotFound(new { success = false, message = "No courses found to export" });

                MemoryStream stream;
                try
                {
                    // Create a copy so GenerateExcel doesn't mutate original list
                    stream = GenerateExcel(courses.Select(c => c).ToList());
                }
                catch (Exception genEx)
                {
                    _logger.LogError(genEx, "Error while generating Excel");
                    return StatusCode(500, new { success = false, message = "Failed to generate Excel file", details = genEx.Message });
                }

                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "Courses.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while exporting courses");
                return StatusCode(500, new { success = false, message = "Unexpected error while exporting courses", details = ex.Message });
            }
        }
    }
}
