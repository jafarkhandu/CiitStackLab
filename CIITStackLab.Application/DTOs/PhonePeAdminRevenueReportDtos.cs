namespace CIITStackLab.Application.DTOs;

public sealed class PhonePeAdminRevenueReportPageDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal SuccessfulRevenue { get; init; }
    public decimal AttemptedAmount { get; init; }
    public decimal FailedAmount { get; init; }
    public decimal PendingAmount { get; init; }
    public int PaymentCount { get; init; }
    public int SuccessfulPaymentCount { get; init; }
    public int FailedPaymentCount { get; init; }
    public int PendingPaymentCount { get; init; }
    public IReadOnlyList<PhonePeAdminPaymentReportRowDto> RecentPayments { get; init; } = Array.Empty<PhonePeAdminPaymentReportRowDto>();
}

public sealed class PhonePeAdminPaymentReportRowDto
{
    public int PaymentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentEmail { get; init; } = string.Empty;
    public string CourseTitle { get; init; } = string.Empty;
    public int CourseId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string MerchantOrderId { get; init; } = string.Empty;
    public string PhonePeOrderId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool CourseAccessGranted { get; init; }
}
