namespace CIITStackLab.Domain.Entities;

public class ContentProgramAnswer
{
    public int Id { get; set; }
    public int? ProgramQuestionId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Flag { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? RestoredAt { get; set; }

    public ContentProgramQuestion? ProgramQuestion { get; set; }
}
