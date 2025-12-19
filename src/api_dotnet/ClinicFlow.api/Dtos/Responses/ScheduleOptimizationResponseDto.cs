namespace ClinicFlow.Api.Dtos.Responses;

using System.Collections.Generic;
using ClinicFlow.Api.Dtos.Requests;

public sealed record ScheduleOptimizationResponseDto(
    BaselineSummaryDto BaselineSummary,
    BaselineSummaryDto BestSummary,
    SummaryDeltaDto Delta,
    StrategyDto RecommendedStrategy,
    IReadOnlyList<CandidateSummaryDto> TopCandidates
);

public sealed record CandidateSummaryDto(
    string StrategyName,
    Dictionary<string, int> OverbookAdditionsBySegment,
    BaselineSummaryDto Summary,
    SummaryDeltaDto Delta
);
