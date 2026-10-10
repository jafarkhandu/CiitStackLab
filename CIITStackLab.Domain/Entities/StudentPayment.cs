namespace CIITStackLab.Domain.Entities;

public class StudentPayment
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal PaymentAmount { get; set; }
    public string? PaymentMode { get; set; }
    public string? PaymentDescription { get; set; }
    public int? Flag { get; set; }
    public bool IsPaid { get; set; }
}
