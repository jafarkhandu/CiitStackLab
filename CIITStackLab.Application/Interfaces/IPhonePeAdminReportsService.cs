using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IPhonePeAdminReportsService
{
    Task<PhonePeAdminRevenueReportPageDto> GetRevenueReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
