namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using System;
using System.Collections.Generic;
using System.Linq;

public static class StrategySimulationService
{
    public static ScheduleSimulationResponseDto Simulate(ScheduleSimulateRequestDto req)
    {
        // Baseline overall
        var (baseExpectedKept, baseRevenue) = BaselineMetricsCalculator.Compute(
            new ScheduleRequestDto(req.ClinicProfile, req.Appointments)
        );

        var capacity = CapacityEstimator.ComputeBlockCapacityAppointments(req.ClinicProfile);
        var (baseOverflow, baseIsOver) = CapacityEstimator.ComputeOverflow(baseExpectedKept, capacity);

        // Baseline per-segment
        var baselineSegments = SegmentMetricsCalculator.ComputeBySegment(
            new ScheduleRequestDto(req.ClinicProfile, req.Appointments),
            capacity
        );

        // Build segment-level avg no-show rate (used for synthetic added slots)
        var segAvgNoShow = req.Appointments
            .GroupBy(SegmentKeyBuilder.Build)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var avg = g.Average(a => (double)a.HistoricalNoShowRate);
                    return Clamp01(avg);
                }
            );

        // Clinic-wide avg no-show fallback
        var clinicAvgNoShow = req.Appointments.Count == 0
            ? 0.2
            : Clamp01(req.Appointments.Average(a => (double)a.HistoricalNoShowRate));

        // Compute simulated additions impact
        double simExpectedKept = baseExpectedKept;
        double simRevenue = baseRevenue;

        // For revenue of added slots: use segment avg unit revenue (fallback overall avg)
        var segAvgRevenue = req.Appointments
            .GroupBy(SegmentKeyBuilder.Build)
            .ToDictionary(
                g => g.Key,
                g => g.Average(a => (double)a.UnitRevenue)
            );

        var overallAvgRevenue = req.Appointments.Count == 0
            ? 0.0
            : req.Appointments.Average(a => (double)a.UnitRevenue);

        // Copy baseline segment objects into a dictionary for easy update
        var simSegMap = baselineSegments.ToDictionary(s => s.SegmentKey, s => s);

        foreach (var kvp in req.Strategy.OverbookAdditionsBySegment)
        {
            var segmentKey = kvp.Key;
            var additions = kvp.Value;

            if (additions <= 0) continue;

            var avgNoShow = segAvgNoShow.TryGetValue(segmentKey, out var segNs) ? segNs : clinicAvgNoShow;
            var pShow = 1.0 - avgNoShow;

            var avgRev = segAvgRevenue.TryGetValue(segmentKey, out var segRev) ? segRev : overallAvgRevenue;

            // Added expected kept and revenue (deterministic)
            simExpectedKept += additions * pShow;
            simRevenue += additions * pShow * avgRev;

            // Update segment summary (if segment exists; if not, create it)
            if (!simSegMap.TryGetValue(segmentKey, out var baseSeg))
            {
                // Segment not in baseline schedule; create a new baseline segment with zeros
                baseSeg = new SegmentSummaryDto(
                    SegmentKey: segmentKey,
                    ExpectedKeptAppointments: 0,
                    BaselineRevenue: 0,
                    CapacityAppointments: Math.Round(capacity, 2),
                    ExpectedOverflowAppointments: 0,
                    IsOverCapacity: false
                );
            }

            var segExpectedKept = baseSeg.ExpectedKeptAppointments + (additions * pShow);
            var segRevenue = baseSeg.BaselineRevenue + (additions * pShow * avgRev);
            var segOverflow = Math.Max(0.0, segExpectedKept - capacity);
            var segIsOver = segOverflow > 0.000001;

            simSegMap[segmentKey] = baseSeg with
            {
                ExpectedKeptAppointments = Math.Round(segExpectedKept, 2),
                BaselineRevenue = Math.Round(segRevenue, 2),
                CapacityAppointments = Math.Round(capacity, 2),
                ExpectedOverflowAppointments = Math.Round(segOverflow, 2),
                IsOverCapacity = segIsOver
            };
        }

        var (simOverflow, simIsOver) = CapacityEstimator.ComputeOverflow(simExpectedKept, capacity);

        // Build segment comparisons (baseline vs simulated)
        var baselineMap = baselineSegments.ToDictionary(s => s.SegmentKey, s => s);
        var allKeys = baselineMap.Keys.Union(simSegMap.Keys).OrderBy(k => k).ToList();

        var comparisons = new List<SegmentComparisonDto>();
        foreach (var key in allKeys)
        {
            var b = baselineMap.TryGetValue(key, out var bs)
                ? bs
                : new SegmentSummaryDto(key, 0, 0, Math.Round(capacity, 2), 0, false);

            var s = simSegMap[key];

            comparisons.Add(new SegmentComparisonDto(key, b, s));
        }

        var baselineSummary = new BaselineSummaryDto(
            ExpectedKeptAppointments: Math.Round(baseExpectedKept, 2),
            BaselineRevenue: Math.Round(baseRevenue, 2),
            BlockCapacityAppointments: Math.Round(capacity, 2),
            ExpectedOverflowAppointments: Math.Round(baseOverflow, 2),
            IsOverCapacity: baseIsOver
        );

        var simulatedSummary = new BaselineSummaryDto(
            ExpectedKeptAppointments: Math.Round(simExpectedKept, 2),
            BaselineRevenue: Math.Round(simRevenue, 2),
            BlockCapacityAppointments: Math.Round(capacity, 2),
            ExpectedOverflowAppointments: Math.Round(simOverflow, 2),
            IsOverCapacity: simIsOver
        );

        var delta = new SummaryDeltaDto(
            DeltaExpectedKeptAppointments: Math.Round(simExpectedKept - baseExpectedKept, 2),
            DeltaRevenue: Math.Round(simRevenue - baseRevenue, 2),
            DeltaExpectedOverflowAppointments: Math.Round(simOverflow - baseOverflow, 2)
        );

        return new ScheduleSimulationResponseDto(
            StrategyName: req.Strategy.Name,
            BaselineSummary: baselineSummary,
            SimulatedSummary: simulatedSummary,
            Delta: delta,
            Segments: comparisons
        );
    }

    private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
}
