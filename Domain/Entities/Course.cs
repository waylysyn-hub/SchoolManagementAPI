namespace Domain.Entities
{
    public class Course
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        // هنا التعديل: TeacherId صار nullable
        public int? TeacherId { get; set; }

        public Teacher? Teacher { get; set; }

        public List<Student> Students { get; set; } = new();
    }
}

