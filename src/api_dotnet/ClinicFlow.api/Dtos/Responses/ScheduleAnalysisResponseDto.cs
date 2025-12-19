namespace ClinicFlow.Api.Dtos.Responses;

// TODO: Implement ScheduleAnalysisResponseDto
public sealed record ScheduleAnalysisResponseDto(
    BaselineSummaryDto Summary,
    IReadOnlyList<SlotPredictionStubDto> SlotPredictions,
    IReadOnlyList<SegmentSummaryDto> Segments
);

public sealed record BaselineSummaryDto(
    double ExpectedKeptAppointments,
    double BaselineRevenue,
    double BlockCapacityAppointments,
    double ExpectedOverflowAppointments,
    bool IsOverCapacity
);

///<summary>
/// Stub for now - later this becomes real model output (p_no_show, CI, drivers).
/// </summary>
public sealed record SlotPredictionStubDto(
    int AppointmentId,
    double NoShowProbability
);

public sealed record SegmentSummaryDto(
    string SegmentKey,
    double ExpectedKeptAppointments,
    double BaselineRevenue,
    double CapacityAppointments,
    double ExpectedOverflowAppointments,
    bool IsOverCapacity
);