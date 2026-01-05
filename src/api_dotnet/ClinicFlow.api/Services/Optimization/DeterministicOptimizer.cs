namespace ClinicFlow.Api.Services.Optimization;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using ClinicFlow.Api.Services.Analysis;
using ClinicFlow.Api.Services.Inference;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class DeterministicOptimizer
{
    /// <summary>
    /// Optimize overbooking strategy using ML predictions and simulation.
    /// </summary>
    public static async Task<ScheduleOptimizationResponseDto> Optimize(
        ScheduleOptimizeRequestDto req,
        IInferenceService inferenceService)
    {
        var core = new ScheduleRequestDto(req.ClinicProfile, req.Appointments);

        // Baseline = simulate with zero additions
        var baselineSimReq = new ScheduleSimulateRequestDto(
            req.ClinicProfile,
            req.Appointments,
            new StrategyDto("Baseline", new Dictionary<string, int>())
        );

        var baseline = await StrategySimulationService.Simulate(baselineSimReq, inferenceService);

        // Determine segments to consider
        var capacity = CapacityEstimator.ComputeBlockCapacityAppointments(req.ClinicProfile);
        var segmentKeys = SegmentMetricsCalculator
            .ComputeBySegment(core, capacity)
            .Select(s => s.SegmentKey)
            .ToList();

        if (req.Search.AllowedSegments is { Count: > 0 })
            segmentKeys = segmentKeys.Where(k => req.Search.AllowedSegments.Contains(k)).ToList();

        // Candidate generation: try single-segment additions first (simple, fast, explainable)
        var candidates = new List<CandidateSummaryDto>();

        foreach (var seg in segmentKeys)
        {
            for (int add = 0; add <= req.Search.MaxAdditionPerSegment; add++)
            {
                if (add > req.Search.MaxTotalAdditions) break;

                var stratMap = new Dictionary<string, int>();
                if (add > 0) stratMap[seg] = add;

                var simReq = new ScheduleSimulateRequestDto(
                    req.ClinicProfile,
                    req.Appointments,
                    new StrategyDto($"Add {add} to {seg}", stratMap)
                );

                var sim = await StrategySimulationService.Simulate(simReq, inferenceService);

                var delta = sim.Delta;

                // Constraint check
                if (sim.SimulatedSummary.ExpectedOverflowAppointments > req.Constraints.MaxExpectedOverflowAppointments)
                    continue;

                if (req.Constraints.MinDeltaRevenue.HasValue &&
                    delta.DeltaRevenue < req.Constraints.MinDeltaRevenue.Value)
                    continue;

                candidates.Add(new CandidateSummaryDto(
                    StrategyName: sim.StrategyName,
                    OverbookAdditionsBySegment: stratMap,
                    Summary: sim.SimulatedSummary,
                    Delta: delta
                ));
            }
        }

        // If nothing passes constraints, return baseline with empty candidates
        if (candidates.Count == 0)
        {
            return new ScheduleOptimizationResponseDto(
                BaselineSummary: baseline.BaselineSummary,
                BestSummary: baseline.BaselineSummary,
                Delta: new SummaryDeltaDto(0, 0, 0),
                RecommendedStrategy: new StrategyDto("Baseline", new Dictionary<string, int>()),
                TopCandidates: Array.Empty<CandidateSummaryDto>()
            );
        }

        // Objective: maximize delta revenue, tie-break by minimizing overflow
        var best = candidates
            .OrderByDescending(c => c.Delta.DeltaRevenue)
            .ThenBy(c => c.Summary.ExpectedOverflowAppointments)
            .First();

        // Provide top N for transparency
        var top = candidates
            .OrderByDescending(c => c.Delta.DeltaRevenue)
            .ThenBy(c => c.Summary.ExpectedOverflowAppointments)
            .Take(10)
            .ToList();

        return new ScheduleOptimizationResponseDto(
            BaselineSummary: baseline.BaselineSummary,
            BestSummary: best.Summary,
            Delta: best.Delta,
            RecommendedStrategy: new StrategyDto(best.StrategyName, best.OverbookAdditionsBySegment),
            TopCandidates: top
        );
    }
}
