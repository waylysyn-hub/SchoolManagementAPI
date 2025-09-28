using ClosedXML.Excel;
using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeachersController : ControllerBase
    {
        private readonly TeacherService _teacherService;

        public TeachersController(TeacherService teacherService)
        {
            _teacherService = teacherService;
        }

        // ===== Excel Generator =====
        private static MemoryStream GenerateExcel(List<TeacherDto> teachers, bool withCourses)
        {
            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Teachers");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "Courses";

            for (int i = 0; i < teachers.Count; i++)
            {
                var t = teachers[i];
                ws.Cell(i + 2, 1).Value = t.Id;
                ws.Cell(i + 2, 2).Value = t.Name;
                ws.Cell(i + 2, 3).Value = t.Email;
                ws.Cell(i + 2, 4).Value = withCourses && t.Courses != null
                    ? string.Join(", ", t.Courses.Select(c => c.Title))
                    : string.Empty;
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        // ===== Get All Teachers with optional name filter and pagination =====
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var teachers = await _teacherService.GetAllAsync(name, page, pageSize);

            if (!teachers.Any())
                return Ok(new { success = true, message = "No teachers found.", data = new List<TeacherDto>() });

            var result = teachers.Select(t => new TeacherDto
            {
                Id = t.Id,
                Name = t.Name,
                Email = t.Email,
                Courses = t.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList()
            }).ToList();

            return Ok(new
            {
                success = true,
                count = result.Count,
                page,
                pageSize,
                data = result
            });
        }

        // ===== Get Teacher By Id =====
        [Authorize(Roles = "Admin,Student,Teacher")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid teacher id. Id must be greater than 0." });

            var teacher = await _teacherService.GetByIdAsync(id);
            if (teacher == null)
                return NotFound(new { success = false, message = $"Teacher with ID {id} not found." });

            var dto = new TeacherDto
            {
                Id = teacher.Id,
                Name = teacher.Name,
                Email = teacher.Email,
                Courses = teacher.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList()
            };

            return Ok(new { success = true, data = dto });
        }

        // ===== Create Teacher =====
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TeacherCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { success = false, message = "Name is required." });

            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            var teacher = new Teacher
            {
                Name = dto.Name,
                Email = dto.Email
            };

            try
            {
                await _teacherService.AddAsync(teacher);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }

            return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, new
            {
                success = true,
                message = "Teacher created successfully.",
                data = new { teacher.Id, teacher.Name, teacher.Email }
            });
        }

        // ===== Update Teacher =====
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] TeacherUpdateDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid teacher id. Id must be greater than 0." });

            var teacher = await _teacherService.GetByIdAsync(id);
            if (teacher == null)
                return NotFound(new { success = false, message = $"Teacher with ID {id} not found." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { success = false, message = "Name is required." });

            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            teacher.Name = dto.Name;
            teacher.Email = dto.Email;

            try
            {
                await _teacherService.UpdateAsync(teacher);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }

            return Ok(new { success = true, message = "Teacher updated successfully." });
        }

        // ===== Delete Teacher =====
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid teacher id. Id must be greater than 0." });

            try
            {
                await _teacherService.DeleteAsync(id);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }

            return Ok(new { success = true, message = "Teacher deleted successfully." });
        }

        // ===== Export to Excel =====
        [Authorize(Roles = "Admin")]
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string? name, [FromQuery] bool withCourses = false)
        {
            var teachers = await _teacherService.GetAllAsync(name, 1, 1000); // Export up to 1000 teachers
            if (!teachers.Any())
                return NotFound(new { success = false, message = "No teachers found to export." });

            var dtos = teachers.Select(t => new TeacherDto
            {
                Id = t.Id,
                Name = t.Name,
                Email = t.Email,
                Courses = t.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList()
            }).ToList();

            var stream = GenerateExcel(dtos, withCourses);
            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Teachers.xlsx");
        }
    }
}
