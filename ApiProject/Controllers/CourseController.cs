using ClosedXML.Excel;
using Data.Services;
using Domain.DTOs;
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

        private static MemoryStream GenerateExcel(List<CourseWithStudentsDto> courses)
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
                ws.Cell(i + 2, 5).Value = string.Join(", ", c.Students.Select(s => s.Name));
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        [Authorize(Policy = "course.read")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? title,
            [FromQuery] int? teacherId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var courses = await _service.GetAllAsync(title, teacherId, page, pageSize);

                if (!courses.Any())
                    return NotFound(new { success = false, message = "No courses found" });

                return Ok(new
                {
                    success = true,
                    count = courses.Count,
                    page,
                    pageSize,
                    data = courses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching courses");
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        [Authorize(Policy = "course.read")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var course = await _service.GetByIdAsync(id);
                if (course == null)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                return Ok(new { success = true, data = course });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching course {CourseId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        [Authorize(Policy = "course.create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CourseCreateDto dto)
        {
            try
            {
                var course = await _service.AddAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = course.Id }, new { success = true, message = "Course created", id = course.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [Authorize(Policy = "course.update")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CourseUpdateDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                if (!updated)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                return Ok(new { success = true, message = $"Course {id} updated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                // هنا بنعالج الأخطاء اللي منطقية متعلقة بالداتا فقط
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                // هنا نتركه للاخطاء غير المتوقعة
                _logger.LogError(ex, "Unexpected error updating course {CourseId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error occurred" });
            }
        }


        [Authorize(Policy = "course.delete")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);
                if (!deleted)
                    return NotFound(new { success = false, message = $"Course {id} not found" });

                return Ok(new { success = true, message = $"Course {id} deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting course {CourseId}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [Authorize(Policy = "course.read")]
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? title,
            [FromQuery] int? teacherId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 1000)
        {
            try
            {
                var courses = await _service.GetAllAsync(title, teacherId, page, pageSize);
                if (!courses.Any())
                    return NotFound(new { success = false, message = "No courses to export" });

                var stream = GenerateExcel(courses);
                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "Courses.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting courses");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
