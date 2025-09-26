using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Data.Services
{
    public class TeacherService
    {
        private readonly BankDbContext _context;

        public TeacherService(BankDbContext context) => _context = context;

        public async Task<List<Teacher>> GetAllAsync() =>
            await _context.Teachers.Include(t => t.Courses).ToListAsync();

        public async Task<Teacher?> GetByIdAsync(int id, bool includeCourses = false)
        {
            if (includeCourses)
                return await _context.Teachers.Include(t => t.Courses)
                                              .FirstOrDefaultAsync(t => t.Id == id);

            return await _context.Teachers.FindAsync(id);
        }

        public async Task AddAsync(Teacher teacher)
        {
            if (await _context.Teachers.AnyAsync(t => t.Email == teacher.Email))
                throw new InvalidOperationException("Email already exists");

            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Teacher teacher)
        {
            if (await _context.Teachers.AnyAsync(t => t.Email == teacher.Email && t.Id != teacher.Id))
                throw new InvalidOperationException("Email already exists");

            _context.Teachers.Update(teacher);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null)
                throw new InvalidOperationException($"Teacher {id} not found");

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
        }
    }
}
