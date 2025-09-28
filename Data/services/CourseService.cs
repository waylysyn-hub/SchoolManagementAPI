using Data;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Services
{
    public class CourseService
    {
        private readonly BankDbContext _context;

        public CourseService(BankDbContext context) => _context = context;

        // ===== Get all courses with optional pagination and filtering =====
        public async Task<List<CourseWithStudentsDto>> GetAllAsync(
            string? title = null,
            int? teacherId = null,
            int page = 1,
            int pageSize = 50)
        {
            var query = _context.Courses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(c => c.Title != null && c.Title.Contains(title));

            if (teacherId.HasValue)
                query = query.Where(c => c.TeacherId == teacherId.Value);

            var courses = await query
                .OrderBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CourseWithStudentsDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId,
                    Students = c.Students.Select(s => new StudentBasicDto
                    {
                        Id = s.Id,
                        Name = s.Name
                    }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            return courses;
        }

        public async Task<CourseWithStudentsDto?> GetByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid course ID", nameof(id));

            return await _context.Courses
                .Where(c => c.Id == id)
                .Select(c => new CourseWithStudentsDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    TeacherId = c.TeacherId,
                    Students = c.Students.Select(s => new StudentBasicDto
                    {
                        Id = s.Id,
                        Name = s.Name
                    }).ToList()
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<Course> AddAsync(CourseCreateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new ArgumentException("Course title is required", nameof(dto));

            if (dto.TeacherId == null || dto.TeacherId <= 0)
                throw new InvalidOperationException("Invalid teacher ID");

            var teacher = await _context.Teachers.FindAsync(dto.TeacherId.Value);
            if (teacher == null)
                throw new InvalidOperationException("Teacher not found");

            var exists = await _context.Courses.AnyAsync(c => c.Title == dto.Title && c.TeacherId == dto.TeacherId);
            if (exists)
                throw new InvalidOperationException("Course already exists for this teacher");

            var course = new Course
            {
                Title = dto.Title,
                Description = dto.Description,
                TeacherId = dto.TeacherId.Value
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return course;
        }

        public async Task<bool> UpdateAsync(int id, CourseUpdateDto dto)
        {
            if (id <= 0) throw new ArgumentException("Invalid course ID", nameof(id));
            ArgumentNullException.ThrowIfNull(dto);

            var course = await _context.Courses
                                       .Include(c => c.Students)
                                       .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return false;

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new ArgumentException("Course title is required", nameof(dto));

            if (dto.TeacherId == null || dto.TeacherId <= 0)
                throw new InvalidOperationException("Invalid teacher ID");

            var teacher = await _context.Teachers.FindAsync(dto.TeacherId.Value);
            if (teacher == null) throw new InvalidOperationException("Teacher not found");

            // ===== تحقق من وجود عنوان مشابه لنفس المعلم =====
            var duplicate = await _context.Courses
                .Where(c => c.Id != id && c.TeacherId == dto.TeacherId)
                .FirstOrDefaultAsync(c => c.Title.ToLower() == dto.Title.ToLower());

            if (duplicate != null)
                throw new InvalidOperationException($"Course '{dto.Title}' already exists for this teacher (Id: {duplicate.Id})");

            // ===== تحديث بيانات الكورس =====
            course.Title = dto.Title;
            course.Description = dto.Description;
            course.TeacherId = dto.TeacherId.Value;

            // ===== تحديث الطلاب المرتبطين بالكورس =====
            if (dto.StudentIds != null)
            {
                if (dto.StudentIds.Count == 0)
                {
                    course.Students.Clear();
                }
                else
                {
                    var students = await _context.Students
                                                 .Where(s => dto.StudentIds.Contains(s.Id))
                                                 .ToListAsync();

                    var missing = dto.StudentIds.Except(students.Select(s => s.Id)).ToList();
                    if (missing.Count > 0)
                        throw new InvalidOperationException($"Students not found: {string.Join(", ", missing)}");

                    course.Students.Clear();
                    foreach (var s in students)
                        course.Students.Add(s);
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }



        public async Task<bool> DeleteAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid course ID", nameof(id));

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return false;

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
