using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Services
{
    public class TeacherService
    {
        private readonly BankDbContext _context;

        public TeacherService(BankDbContext context) => _context = context;

        // ===== Get all with optional name filter and pagination =====
        public async Task<List<Teacher>> GetAllAsync(string? name = null, int page = 1, int pageSize = 50)
        {
            var query = _context.Teachers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(t => t.Name.Contains(name));

            return await query
                .OrderBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new Teacher
                {
                    Id = t.Id,
                    Name = t.Name,
                    Email = t.Email,
                    Courses = t.Courses.Select(c => new Course
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Description = c.Description,
                        TeacherId = c.TeacherId
                    }).ToList()
                })
                .ToListAsync();
        }

        // ===== Get teacher by ID =====
        public async Task<Teacher?> GetByIdAsync(int id)
        {
            return await _context.Teachers
                                 .Where(t => t.Id == id)
                                 .Select(t => new Teacher
                                 {
                                     Id = t.Id,
                                     Name = t.Name,
                                     Email = t.Email,
                                     Courses = t.Courses.Select(c => new Course
                                     {
                                         Id = c.Id,
                                         Title = c.Title,
                                         Description = c.Description,
                                         TeacherId = c.TeacherId
                                     }).ToList()
                                 }).FirstOrDefaultAsync();
        }

        // ===== Add teacher with email validation =====
        public async Task AddAsync(Teacher teacher)
        {
            if (!IsValidEmail(teacher.Email))
                throw new InvalidOperationException("Invalid email format");

            if (await _context.Teachers.AnyAsync(t => t.Email == teacher.Email))
                throw new InvalidOperationException("Email already exists");

            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();
        }

        // ===== Update teacher with email validation =====
        public async Task UpdateAsync(Teacher teacher)
        {
            if (!IsValidEmail(teacher.Email))
                throw new InvalidOperationException("Invalid email format");

            if (await _context.Teachers.AnyAsync(t => t.Email == teacher.Email && t.Id != teacher.Id))
                throw new InvalidOperationException("Email already exists");

            _context.Teachers.Update(teacher);
            await _context.SaveChangesAsync();
        }

        // ===== Delete teacher =====
        public async Task DeleteAsync(int id)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null)
                throw new InvalidOperationException($"Teacher {id} not found");

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
        }

        // ===== Email validator =====
        private static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) &&
                   System.Text.RegularExpressions.Regex.IsMatch(
                       email,
                       @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                       System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
