using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminTopicIndexViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminTopicDto> ActiveTopics { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminTopicDto>();

    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminArchivedTopicDto> ArchivedTopics { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminArchivedTopicDto>();

    public AdminTopicFormModel? EditTopic { get; init; }
}

public sealed class AdminTopicFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Topic name is required.")]
    [StringLength(100, ErrorMessage = "Topic name cannot exceed 100 characters.")]
    [Display(Name = "Topic name")]
    public string TopicName { get; set; } = string.Empty;

    [Display(Name = "Public folder ID")]
    public string? PublicFolderId { get; set; }
}
