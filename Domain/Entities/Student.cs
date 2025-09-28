using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Student
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = null!;

        [MaxLength(100)]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Course> Courses { get; set; } = new List<Course>();

        // ===========================
        // Image Options
        // ===========================
        // Path if saving in folder
        public string? PhotoPath { get; set; }

        // Data if saving in DB
        public byte[]? PhotoData { get; set; }
        public string? PhotoContentType { get; set; }
    }
}
