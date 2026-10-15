namespace CIITStackLab.WebApp.Models;

public sealed class CoursePurchaseHistoryViewModel
{
    public int PurchaseId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime PurchasedAt { get; init; }
}
