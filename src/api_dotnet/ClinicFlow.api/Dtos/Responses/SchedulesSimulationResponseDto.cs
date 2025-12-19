namespace ClinicFlow.Api.Dtos.Responses;

public sealed record ScheduleSimulationResponseDto(
    string StrategyName,
    BaselineSummaryDto BaselineSummary,
    BaselineSummaryDto SimulatedSummary,
    SummaryDeltaDto Delta,
    IReadOnlyList<SegmentComparisonDto> Segments
);

public sealed record SummaryDeltaDto(
    double DeltaExpectedKeptAppointments,
    double DeltaRevenue,
    double DeltaExpectedOverflowAppointments
);

public sealed record SegmentComparisonDto(
    string SegmentKey,
    SegmentSummaryDto Baseline,
    SegmentSummaryDto Simulated
);