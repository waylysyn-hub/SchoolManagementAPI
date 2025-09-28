using Domain.Entities;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Services
{
    public class StudentService
    {
        private readonly BankDbContext _context;
        private readonly ILogger<StudentService> _logger;

        public StudentService(BankDbContext context, ILogger<StudentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Existing method (kept but make AsNoTracking to reduce tracking overhead)
        public async Task<List<Student>> FilterAsync(string? name = null, string? email = null, List<int>? courseIds = null, int? studentId = null)
        {
            var query = _context.Students.Include(s => s.Courses).AsNoTracking().AsQueryable();

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

        // NEW: Paged lightweight list (does NOT load PhotoData)
        public async Task<(List<StudentListDto> Items, int Total)> GetStudentsPagedAsync(string? name = null, string? email = null, List<int>? courseIds = null, int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var query = _context.Students.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{name}%"));
            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(s => EF.Functions.Like(s.Email, $"%{email}%"));
            if (courseIds != null && courseIds.Any())
                query = query.Where(s => s.Courses.Any(c => courseIds.Contains(c.Id)));

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StudentListDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Email = s.Email,
                    PhotoPath = s.PhotoPath,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    Courses = s.Courses.Select(c => new CourseDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Description = c.Description,
                        TeacherId = c.TeacherId
                    }).ToList()
                })
                .ToListAsync();

            return (items, total);
        }

        // NEW: For export - lightweight DTOs, no PhotoData
        public async Task<List<StudentExportDto>> GetStudentsForExportAsync(int? studentId = null, List<int>? courseIds = null)
        {
            var query = _context.Students.AsNoTracking().AsQueryable();

            if (studentId.HasValue)
                query = query.Where(s => s.Id == studentId.Value);

            if (courseIds != null && courseIds.Any())
                query = query.Where(s => s.Courses.Any(c => courseIds.Contains(c.Id)));

            var list = await query
                .OrderByDescending(s => s.Id)
                .Select(s => new StudentExportDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Email = s.Email,
                    CourseTitles = s.Courses.Select(c => c.Title).ToList()
                })
                .ToListAsync();

            return list;
        }

        public async Task<Student?> GetByIdAsync(int id) =>
            await _context.Students.Include(s => s.Courses).FirstOrDefaultAsync(s => s.Id == id);

        public async Task AddAsync(Student student, List<int>? courseIds = null)
        {
            if (await _context.Students.AnyAsync(s => s.Email == student.Email))
                throw new InvalidOperationException("Email already exists");

            if (courseIds != null && courseIds.Any())
            {
                var courses = await _context.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync();
                var missingIds = courseIds.Except(courses.Select(c => c.Id)).ToList();
                if (missingIds.Any())
                    throw new InvalidOperationException($"Courses not found: {string.Join(", ", missingIds)}");
                student.Courses = courses;
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateAsync(
            Student student,
            List<int>? courseIds = null,
            string? newPhotoPath = null,
            byte[]? newPhotoData = null,
            string? newPhotoContentType = null,
            bool forceReplacePhoto = false)
        {
            var existing = await _context.Students.Include(s => s.Courses)
                                                  .FirstOrDefaultAsync(s => s.Id == student.Id);

            if (existing == null)
                throw new InvalidOperationException($"Student {student.Id} not found");

            // تحقق من الإيميل
            bool emailExists = await _context.Students
                                             .AnyAsync(s => s.Email == student.Email && s.Id != student.Id);
            if (emailExists)
                throw new InvalidOperationException("Email already exists");

            existing.Name = student.Name;
            existing.Email = student.Email;
            existing.UpdatedAt = student.UpdatedAt;

            // معالجة الصورة
            if (!string.IsNullOrEmpty(newPhotoPath) || newPhotoData != null)
            {
                if (!forceReplacePhoto && (!string.IsNullOrEmpty(existing.PhotoPath) || existing.PhotoData != null))
                    throw new InvalidOperationException("Student already has a photo. Set ForceReplacePhoto=true to replace it.");

                // مسح الصورة القديمة من القرص إذا موجودة
                if (!string.IsNullOrEmpty(existing.PhotoPath) && System.IO.File.Exists(existing.PhotoPath))
                {
                    try { System.IO.File.Delete(existing.PhotoPath); } catch { }
                }

                existing.PhotoPath = newPhotoPath;
                existing.PhotoData = newPhotoData;
                existing.PhotoContentType = newPhotoContentType;
            }
            else if (string.IsNullOrEmpty(newPhotoPath) && newPhotoData == null)
            {
                // لم تُرسل صورة → إزالة البيانات
                existing.PhotoPath = null;
                existing.PhotoData = null;
                existing.PhotoContentType = null;
            }

            // تحديث الدورات
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




        public async Task DeleteAsync(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                throw new InvalidOperationException($"Student {id} not found");

            // delete file if exists, but wrap exceptions to log & continue
            if (!string.IsNullOrEmpty(student.PhotoPath) && File.Exists(student.PhotoPath))
            {
                try
                {
                    File.Delete(student.PhotoPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete photo file for student {StudentId}", id);
                    // continue with deletion of db record (or throw if you want strict behavior)
                }
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePhotoAsync(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                throw new InvalidOperationException($"Student {studentId} not found");

            var deleted = false;

            // إذا الصورة محفوظة كملف
            if (!string.IsNullOrEmpty(student.PhotoPath) && File.Exists(student.PhotoPath))
            {
                try
                {
                    File.Delete(student.PhotoPath);
                    student.PhotoPath = null;
                    deleted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete photo file for student {StudentId}", studentId);
                    throw new InvalidOperationException("Could not delete the photo file (check permissions).");
                }
            }

            // إذا الصورة محفوظة بالـ DB
            if (student.PhotoData != null || student.PhotoContentType != null)
            {
                student.PhotoData = null;
                student.PhotoContentType = null;
                deleted = true;
            }

            if (!deleted)
                throw new InvalidOperationException("Student does not have a photo to delete.");

            await _context.SaveChangesAsync();
        }

    }
}
