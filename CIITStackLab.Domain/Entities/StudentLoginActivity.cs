namespace CIITStackLab.Domain.Entities;

public class StudentLoginActivity
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime? LogoutTime { get; set; }
    public string? IpAddress { get; set; }
    public int? Flag { get; set; }
}
