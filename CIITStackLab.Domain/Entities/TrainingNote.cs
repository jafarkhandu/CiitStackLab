namespace CIITStackLab.Domain.Entities;

public class TrainingNote
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? Flag { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Topic Topic { get; set; } = null!;
}
