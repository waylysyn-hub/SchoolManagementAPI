using System.Text.Json.Serialization;

namespace Domain.DTOs
{
    public class CourseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; } // ✅ Nullable لتجنب الدوران
    }

    // ===== لا يحتوي على StudentIds لإنشاء الكورس =====
    public class CourseCreateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; } // ✅ Nullable
    }

    // ===== يحتوي على StudentIds فقط للتحديث =====
    public class CourseUpdateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
        [JsonIgnore]
        public List<int>? StudentIds { get; set; } // اختياري لتحديث الطلاب
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
