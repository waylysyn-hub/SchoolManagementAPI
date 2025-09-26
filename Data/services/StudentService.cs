using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Services
{
    public class StudentService
    {
        private readonly BankDbContext _context;

        public StudentService(BankDbContext context) => _context = context;

        // ===== Filtered GetAll =====
        public async Task<List<Student>> FilterAsync(string? name = null, string? email = null, List<int>? courseIds = null, int? studentId = null)
        {
            var query = _context.Students.Include(s => s.Courses).AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{name}%"));

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(s => EF.Functions.Like(s.Email, $"%{email}%"));

            if (studentId.HasValue)
                query = query.Where(s => s.Id == studentId.Value);

            if (courseIds != null && courseIds.Any())
                query = query.Where(s => s.Courses.Any(c => courseIds.Contains(c.Id)));

            return await query.OrderByDescending(s => s.Id).ToListAsync();
        }

        public async Task<Student?> GetByIdAsync(int id) =>
            await _context.Students.Include(s => s.Courses).FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<Student>> GetByIdsAsync(List<int> ids) =>
            await _context.Students.Include(s => s.Courses).Where(s => ids.Contains(s.Id)).ToListAsync();

        public async Task AddAsync(Student student, List<int>? courseIds = null)
        {
            if (await _context.Students.AnyAsync(s => s.Email == student.Email))
                throw new InvalidOperationException("Email already exists");

            if (courseIds != null && courseIds.Any())
            {
                var courses = await _context.Courses
                                            .Where(c => courseIds.Contains(c.Id))
                                            .ToListAsync();

                var missingIds = courseIds.Except(courses.Select(c => c.Id)).ToList();
                if (missingIds.Any())
                    throw new InvalidOperationException($"Courses not found: {string.Join(", ", missingIds)}");

                student.Courses = courses;
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Student student, List<int>? courseIds = null)
        {
            var existing = await _context.Students.Include(s => s.Courses).FirstOrDefaultAsync(s => s.Id == student.Id);
            if (existing == null) throw new InvalidOperationException($"Student {student.Id} not found");

            if (await _context.Students.AnyAsync(s => s.Email == student.Email && s.Id != student.Id))
                throw new InvalidOperationException("Email already exists");

            existing.Name = student.Name;
            existing.Email = student.Email;
            existing.UpdatedAt = DateTime.UtcNow;

            if (courseIds != null)
            {
                var courses = await _context.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync();
                var missingIds = courseIds.Except(courses.Select(c => c.Id)).ToList();
                if (missingIds.Any())
                    throw new InvalidOperationException($"Courses not found: {string.Join(", ", missingIds)}");

                existing.Courses = courses.Distinct().ToList();
            }

            await _context.SaveChangesAsync();
        }

        public async Task AddCourseToStudentAsync(int studentId, int courseId)
        {
            var student = await GetByIdAsync(studentId);
            if (student == null) throw new InvalidOperationException($"Student {studentId} not found");

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) throw new InvalidOperationException($"Course {courseId} not found");

            if (!student.Courses.Any(c => c.Id == courseId))
            {
                student.Courses.Add(course);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveCourseFromStudentAsync(int studentId, int courseId)
        {
            var student = await GetByIdAsync(studentId);
            if (student == null) throw new InvalidOperationException($"Student {studentId} not found");

            var course = student.Courses.FirstOrDefault(c => c.Id == courseId);
            if (course == null)
                throw new InvalidOperationException($"Course {courseId} not found for student {studentId}");

            student.Courses.Remove(course);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Student {id} not found");
            }
        }
    }
}
