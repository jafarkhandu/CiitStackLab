namespace CIITStackLab.Domain.Entities;

public class Course
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public double? FeesAmount { get; set; }

    public DateTime? FeesChangeDate { get; set; }

    public double? InstallmentPercentage { get; set; }

    public int? Flag { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? RestoredAt { get; set; }

    public ICollection<CourseModule> Modules { get; set; } = new List<CourseModule>();
}
