namespace CIITStackLab.Domain.Entities;

public sealed class TopicExamQuestion
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Option1 { get; set; } = string.Empty;
    public string Option2 { get; set; } = string.Empty;
    public string Option3 { get; set; } = string.Empty;
    public string Option4 { get; set; } = string.Empty;
    public int CorrectOptionNumber { get; set; }
    public int Flag { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
