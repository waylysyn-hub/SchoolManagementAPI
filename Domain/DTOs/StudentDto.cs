using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CourseDto> Courses { get; set; } = new(); // non-nullable
    }

    public class StudentCreateDto
    {
        [Required] public string Name { get; set; } = null!;
        [Required, EmailAddress(ErrorMessage = "Invalid email format")] public string Email { get; set; } = null!;
        public List<int>? CourseIds { get; set; }
    }

    public class StudentUpdateDto
    {
        [Required] public string Name { get; set; } = null!;
        [Required, EmailAddress(ErrorMessage = "Invalid email format")] public string Email { get; set; } = null!;
        public List<int>? CourseIds { get; set; }
    }

    public class StudentFilterDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public List<int>? CourseIds { get; set; }
    }

    public class StudentBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
