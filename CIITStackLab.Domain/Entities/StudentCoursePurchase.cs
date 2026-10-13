namespace CIITStackLab.Domain.Entities;

public sealed class StudentCoursePurchase
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public decimal PurchasePrice { get; set; }

    public DateTime PurchasedAt { get; set; }
}
