namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SegmentMetricsCalculator
{
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

    private static double Clamp01(float x)
    {
        if (x < 0f) return 0.0;
        if (x > 1f) return 1.0;
        return x;
    }
}