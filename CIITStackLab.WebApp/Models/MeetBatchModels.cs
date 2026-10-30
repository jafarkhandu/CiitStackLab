using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class MeetBatchIndexViewModel
{
    public IReadOnlyList<MeetBatchListItemViewModel> ActiveBatches { get; init; } = [];
    public IReadOnlyList<MeetBatchListItemViewModel> ArchivedBatches { get; init; } = [];
    public IReadOnlyList<MeetStudentOptionViewModel> Students { get; init; } = [];
}

public sealed class MeetBatchListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class MeetStudentOptionViewModel
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
}

public sealed class MeetBatchDetailsViewModel
{
    public int Id { get; init; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string BatchCode { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<MeetStudentOptionViewModel> Students { get; init; } = [];
    public List<string> SelectedStudentIds { get; set; } = [];
}
