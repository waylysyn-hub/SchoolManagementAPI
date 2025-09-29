using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Teacher
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        [MaxLength(100)]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }
        // علاقة: أستاذ عندو عدة كورسات
        public List<Course> Courses { get; set; } = new List<Course>();
    }
}
