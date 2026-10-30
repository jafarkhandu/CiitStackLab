namespace CIITStackLab.Domain.Entities;

public sealed class MeetSessionParticipant
{
    public int Id { get; set; }
    public int MeetSessionId { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? AdmittedAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
    public bool CanUseMicrophone { get; set; } = true;
    public bool CanUseCamera { get; set; } = true;
    public bool CanShareScreen { get; set; }

    public MeetSession MeetSession { get; set; } = null!;
}
