using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhotoPath { get; set; }   // لازم تنضاف
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CourseDto> Courses { get; set; } = new(); // non-nullable
    }

    public class StudentCreateDto
    {
        [Required]
        public string Name { get; set; } = null!;

        [Required, EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        public List<int>? CourseIds { get; set; }

        // ===========================
        // Image Upload
        // ===========================
        public IFormFile? Photo { get; set; }
        public bool SaveInDatabase { get; set; } = false; // true = DB, false = file path
    }

    public class StudentUpdateDto
    {
        [Required]
        public string Name { get; set; } = null!;

        [Required, EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        public List<int>? CourseIds { get; set; }

        // ===========================
        // Image Upload
        // ===========================
        public IFormFile? Photo { get; set; }
        public bool SaveInDatabase { get; set; } = false; // true = DB, false = file path
        public bool ForceReplacePhoto { get; set; } = false;
    }
    public class StudentListDto  // lightweight for lists/paging (no PhotoData)
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CourseDto> Courses { get; set; } = new();
    }
    public class StudentFilterDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public List<int>? CourseIds { get; set; }
    }
    public class StudentExportDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> CourseTitles { get; set; } = new();
    }
    public class StudentBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
