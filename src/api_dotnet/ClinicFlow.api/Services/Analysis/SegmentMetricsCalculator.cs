namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SegmentMetricsCalculator
{
    /// <summary>
    /// Compute segment metrics using historical no-show rates (baseline method).
    /// </summary>
    public static IReadOnlyList<SegmentSummaryDto> ComputeBySegment(
        ScheduleRequestDto req,
        double capacityPerBlock
    )
    {
        //Group appointments by segment key (DayOfWeek-TimeBlock)
        var grouped = req.Appointments.GroupBy(SegmentKeyBuilder.Build).OrderBy(g => g.Key);

        var results = new List<SegmentSummaryDto>();

        foreach(var group in grouped)
        {
            double expectedKept = 0.0;
            double baselineRevenue = 0.0;

            foreach(var a in group)
            {
                var pShow = 1.0 - Clamp01(a.HistoricalNoShowRate);
                expectedKept += pShow;
                baselineRevenue += pShow * a.UnitRevenue;
            }
            var overflow = Math.Max(0.0, expectedKept - capacityPerBlock);
            var isOver = overflow > 0.000001;

            results.Add(new SegmentSummaryDto(
                SegmentKey: group.Key,
                ExpectedKeptAppointments: Math.Round(expectedKept, 2),
                BaselineRevenue: Math.Round(baselineRevenue, 2),
                CapacityAppointments: Math.Round(capacityPerBlock, 2),
                ExpectedOverflowAppointments: Math.Round(overflow, 2),
                IsOverCapacity: isOver
            ));
        }
        return results;
    }

    /// <summary>
    /// Compute segment metrics using ML predictions.
    /// </summary>
    public static IReadOnlyList<SegmentSummaryDto> ComputeBySegmentWithPredictions(
        ScheduleRequestDto req,
        double capacityPerBlock,
        IReadOnlyList<SlotPredictionStubDto> predictions)
    {
        // Create lookup dictionary by appointment ID
        var predLookup = predictions.ToDictionary(p => p.AppointmentId, p => p.NoShowProbability);

        //Group appointments by segment key (DayOfWeek-TimeBlock)
        var grouped = req.Appointments.GroupBy(SegmentKeyBuilder.Build).OrderBy(g => g.Key);

        var results = new List<SegmentSummaryDto>();

        foreach(var group in grouped)
        {
            double expectedKept = 0.0;
            double baselineRevenue = 0.0;

            foreach(var a in group)
            {
                var pNoShow = predLookup.GetValueOrDefault(a.AppointmentId, a.HistoricalNoShowRate);
                var pShow = 1.0 - Clamp01(pNoShow);
                expectedKept += pShow;
                baselineRevenue += pShow * a.UnitRevenue;
            }
            var overflow = Math.Max(0.0, expectedKept - capacityPerBlock);
            var isOver = overflow > 0.000001;

            results.Add(new SegmentSummaryDto(
                SegmentKey: group.Key,
                ExpectedKeptAppointments: Math.Round(expectedKept, 2),
                BaselineRevenue: Math.Round(baselineRevenue, 2),
                CapacityAppointments: Math.Round(capacityPerBlock, 2),
                ExpectedOverflowAppointments: Math.Round(overflow, 2),
                IsOverCapacity: isOver
            ));
        }
        return results;
    }

    private static double Clamp01(double x)
    {
        if (x < 0.0) return 0.0;
        if (x > 1.0) return 1.0;
        return x;
    }
}