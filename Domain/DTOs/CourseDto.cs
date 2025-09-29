using System.Text.Json.Serialization;

namespace Domain.DTOs
{
    public class CourseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
    }

    public class CourseCreateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
    }

    public class CourseUpdateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
        [JsonIgnore]
        public List<int>? StudentIds { get; set; }
    }

    public class CourseDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
        public List<StudentDto> Students { get; set; } = new();
    }

    public class CourseWithStudentsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
        public List<StudentBasicDto> Students { get; set; } = new();
    }
}
