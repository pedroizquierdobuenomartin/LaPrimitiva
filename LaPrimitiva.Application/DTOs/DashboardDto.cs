namespace LaPrimitiva.Application.DTOs;

public sealed record DashboardDto(
    SummaryDto Summary,
    IReadOnlyList<MonthlySummaryDto> MonthlySummaries);
