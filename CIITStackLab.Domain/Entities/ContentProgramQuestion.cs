namespace CIITStackLab.Domain.Entities;

public class ContentProgramQuestion
{
    public int Id { get; set; }
    public int? ContentId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public string QuestionDescription { get; set; } = string.Empty;
    public int? Flag { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? RestoredAt { get; set; }

    public Lesson? Content { get; set; }
    public ICollection<ContentProgramAnswer> Answers { get; set; } = new List<ContentProgramAnswer>();
}
