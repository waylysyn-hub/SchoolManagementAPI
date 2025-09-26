using ClosedXML.Excel;
using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly CourseService _courseService;

        public StudentsController(StudentService studentService, CourseService courseService)
        {
            _studentService = studentService;
            _courseService = courseService;
        }

        // ===== Helper: Generate Excel =====
        private static MemoryStream GenerateExcel(List<Student> students)
        {
            var workbook = new XLWorkbook();
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
                ws.Cell(i + 2, 4).Value = s.Courses != null && s.Courses.Any()
                    ? string.Join(", ", s.Courses.Select(c => c.Title))
                    : "";
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        // ===== GET /students =====
        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? name, [FromQuery] string? email, [FromQuery] List<int>? courseIds)
        {
            var students = await _studentService.FilterAsync(name, email, courseIds);

            if (!students.Any())
                return NotFound(new { success = false, message = "No students found with the given filters" });

            var result = students.Select(s => new StudentDto
            {
                Id = s.Id,
                Name = s.Name,
                Email = s.Email,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Courses = s.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList()
            });

            return Ok(new { success = true, count = result.Count(), data = result });
        }

        // ===== GET /students/{id} =====
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
                Courses = student.Courses?.Select(c => new CourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId
                }).ToList()
            };

            return Ok(new { success = true, data = dto });
        }

        // ===== POST /students =====
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StudentCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid input", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var student = new Student
            {
                Name = dto.Name,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _studentService.AddAsync(student, dto.CourseIds);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }

            return CreatedAtAction(nameof(GetById), new { id = student.Id }, new { success = true, message = "Student created successfully", id = student.Id });
        }

        // ===== PUT /students/{id} =====
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] StudentUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid input", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var student = new Student
            {
                Id = id,
                Name = dto.Name,
                Email = dto.Email
            };

            try
            {
                await _studentService.UpdateAsync(student, dto.CourseIds);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }

            return Ok(new { success = true, message = $"Student {id} updated successfully" });
        }

        // ===== DELETE /students/{id} =====
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
        }

        // ===== GET /students/export =====
        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] int? studentId, [FromQuery] List<int>? courseIds)
        {
            var students = await _studentService.FilterAsync(studentId: studentId, courseIds: courseIds);

            if (!students.Any())
                return NotFound(new { success = false, message = "No students found to export" });

            var stream = GenerateExcel(students);
            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Students.xlsx");
        }
    }
}
