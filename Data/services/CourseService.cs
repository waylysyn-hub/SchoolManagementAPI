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

        public async Task<List<Course>> GetAllAsync() =>
            await _context.Courses.Include(c => c.Students).ToListAsync();

        public async Task<Course?> GetByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid course ID", nameof(id));

            return await _context.Courses
                                 .Include(c => c.Students)
                                 .FirstOrDefaultAsync(c => c.Id == id);
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

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new ArgumentException("Course title is required", nameof(dto));

            if (dto.TeacherId == null || dto.TeacherId <= 0)
                throw new InvalidOperationException("Invalid teacher ID");

            var course = await _context.Courses
                                       .Include(c => c.Students)
                                       .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return false;

            var teacher = await _context.Teachers.FindAsync(dto.TeacherId.Value);
            if (teacher == null) throw new InvalidOperationException("Teacher not found");

            var duplicate = await _context.Courses.AnyAsync(c => c.Id != id && c.Title == dto.Title && c.TeacherId == dto.TeacherId);
            if (duplicate) throw new InvalidOperationException("Another course with the same title already exists for this teacher");

            course.Title = dto.Title;
            course.Description = dto.Description;
            course.TeacherId = dto.TeacherId.Value;

            // تحديث الطلاب فقط إذا موجود StudentIds
            if (dto.StudentIds != null)
            {
                if (dto.StudentIds.Count > 0)
                {
                    var students = await _context.Students.Where(s => dto.StudentIds.Contains(s.Id)).ToListAsync();
                    var missing = dto.StudentIds.Except(students.Select(s => s.Id)).ToList();
                    if (missing.Count > 0)
                        throw new InvalidOperationException($"Students not found: {string.Join(", ", missing)}");

                    course.Students = students;
                }
                else
                {
                    // إذا مصفوفة فارغة => إزالة جميع الطلاب
                    course.Students = new List<Student>();
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
