using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminTopicIndexViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminTopicDto> ActiveTopics { get; init; } = Array.Empty<CIITStackLab.Application.DTOs.AdminTopicDto>();
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminArchivedTopicDto> ArchivedTopics { get; init; } = Array.Empty<CIITStackLab.Application.DTOs.AdminArchivedTopicDto>();
    public AdminTopicFormModel? EditTopic { get; init; }
}

public sealed class AdminTopicFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Topic name is required.")]
    [StringLength(100, ErrorMessage = "Topic name cannot exceed 100 characters.")]
    public string TopicName { get; set; } = string.Empty;

    public string? PublicFolderId { get; set; }

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    [Range(1, 100000, ErrorMessage = "Duration must be between 1 and 100000 minutes.")]
    public int? DurationMinutes { get; set; }
}
