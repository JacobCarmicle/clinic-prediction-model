namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using ClinicFlow.Api.Services.Simulation;
using ClinicFlow.Api.Services.Inference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class StrategySimulationService
{
    private static readonly SimulationService _simulationService = new();

    /// <summary>
    /// Simulate schedule outcomes with overbooking strategy using ML predictions.
    /// Falls back to historical rates if ML service unavailable.
    /// </summary>
    public static async Task<ScheduleSimulationResponseDto> Simulate(
        ScheduleSimulateRequestDto req,
        IInferenceService inferenceService)
    {
        // Build segment mapping: segment key -> segment ID (byte)
        var segmentKeys = req.Appointments
            .Select(SegmentKeyBuilder.Build)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        var segmentKeyToId = segmentKeys
            .Select((key, idx) => new { key, id = (byte)idx })
            .ToDictionary(x => x.key, x => x.id);

        // Get ML predictions (falls back to historical rates if ML unavailable)
        var scheduleRequest = new ScheduleRequestDto(req.ClinicProfile, req.Appointments);
        var mlPredictions = await inferenceService.PredictAsync(scheduleRequest);
        
        // Create lookup dictionary for ML predictions
        var predLookup = mlPredictions.ToDictionary(p => p.AppointmentId, p => p.NoShowProbability);

        // Convert appointments to simulation format with segment IDs
        // Phase C: Use ML predictions instead of historical rates
        var simAppointments = req.Appointments.Select(a => new SimulationAppointment
        {
            NoShowProbability = (float)predLookup.GetValueOrDefault(a.AppointmentId, a.HistoricalNoShowRate),
            UnitRevenue = a.UnitRevenue,
            ServiceMinutes = null, // Use default from config
            SegmentId = segmentKeyToId[SegmentKeyBuilder.Build(a)]
        }).ToArray();

        var simConfig = new SimulationConfig
        {
            ProvidersPerBlock = req.ClinicProfile.ProvidersPerBlock,
            AvgServiceMinutes = req.ClinicProfile.AvgServiceMinutes,
            TimeBlockMinutes = req.ClinicProfile.TimeBlockMinutes,
            NumTrials = 5000, // Default Monte Carlo trials
            RandomSeed = 12345 // Fixed seed for reproducibility
        };

        // Run baseline simulation (no overbooking)
        var baselineRequest = new SimulationRequest { Appointments = simAppointments };
        var baselineResult = _simulationService.RunSimulation(baselineRequest, simConfig);

        var capacity = CapacityEstimator.ComputeBlockCapacityAppointments(req.ClinicProfile);
        var baselineOverflow = Math.Max(0.0, baselineResult.ExpectedKept - capacity);
        var baselineIsOver = baselineOverflow > 0.000001;

        // Build baseline segments using ML predictions for consistency
        var baselineSegments = SegmentMetricsCalculator.ComputeBySegmentWithPredictions(
            new ScheduleRequestDto(req.ClinicProfile, req.Appointments),
            capacity,
            mlPredictions
        );

        // Prepare strategy overbooking additions
        var nSegments = (ushort)segmentKeys.Count;
        var strategyAdditions = new ushort[nSegments];
        
        foreach (var kvp in req.Strategy.OverbookAdditionsBySegment)
        {
            if (segmentKeyToId.TryGetValue(kvp.Key, out var segId))
            {
                strategyAdditions[segId] = (ushort)kvp.Value;
            }
        }

        // Run simulation with strategy overbooking using batch mode
        // We create a single strategy array for batch mode
        var strategyArray = new ushort[][] { strategyAdditions };
        var simulatedResults = _simulationService.RunSimulationBatch(
            baselineRequest,
            simConfig,
            strategyArray,
            nSegments
        );

        var simulatedResult = simulatedResults[0];
        var simulatedOverflow = Math.Max(0.0, simulatedResult.ExpectedKept - capacity);
        var simulatedIsOver = simulatedOverflow > 0.000001;

        // Build segment-level comparisons
        // For segments with overbooking, we need to compute the impact
        var baselineMap = baselineSegments.ToDictionary(s => s.SegmentKey, s => s);
        var simSegMap = new Dictionary<string, SegmentSummaryDto>();

        // Calculate simulated segment metrics
        foreach (var segKey in segmentKeys)
        {
            var segId = segmentKeyToId[segKey];
            var additions = strategyAdditions[segId];

            var baseSeg = baselineMap.TryGetValue(segKey, out var bs)
                ? bs
                : new SegmentSummaryDto(
                    SegmentKey: segKey,
                    ExpectedKeptAppointments: 0,
                    BaselineRevenue: 0,
                    CapacityAppointments: Math.Round(capacity, 2),
                    ExpectedOverflowAppointments: 0,
                    IsOverCapacity: false
                );

            // For simulated segment, we approximate by adding expected impact of overbooking
            // This is a simplification - in a full implementation, we'd run per-segment simulations
            var segAppointments = req.Appointments
                .Where(a => SegmentKeyBuilder.Build(a) == segKey)
                .ToList();

            // Use ML predictions for segment average (more accurate than historical rates)
            var segAvgNoShow = segAppointments.Count > 0
                ? segAppointments.Average(a => predLookup.GetValueOrDefault(a.AppointmentId, a.HistoricalNoShowRate))
                : 0.2;
            var segAvgRevenue = segAppointments.Count > 0
                ? segAppointments.Average(a => (double)a.UnitRevenue)
                : 0.0;

            var pShow = 1.0 - segAvgNoShow;
            var additionalKept = additions * pShow;
            var additionalRevenue = additions * pShow * segAvgRevenue;

            var simSegExpectedKept = baseSeg.ExpectedKeptAppointments + additionalKept;
            var simSegRevenue = baseSeg.BaselineRevenue + additionalRevenue;
            var simSegOverflow = Math.Max(0.0, simSegExpectedKept - capacity);
            var simSegIsOver = simSegOverflow > 0.000001;

            simSegMap[segKey] = new SegmentSummaryDto(
                SegmentKey: segKey,
                ExpectedKeptAppointments: Math.Round(simSegExpectedKept, 2),
                BaselineRevenue: Math.Round(simSegRevenue, 2),
                CapacityAppointments: Math.Round(capacity, 2),
                ExpectedOverflowAppointments: Math.Round(simSegOverflow, 2),
                IsOverCapacity: simSegIsOver
            );
        }

        // Build comparisons
        var allKeys = baselineMap.Keys.Union(simSegMap.Keys).OrderBy(k => k).ToList();
        var comparisons = allKeys.Select(key =>
        {
            var b = baselineMap.TryGetValue(key, out var bs)
                ? bs
                : new SegmentSummaryDto(key, 0, 0, Math.Round(capacity, 2), 0, false);

            var s = simSegMap.TryGetValue(key, out var ss)
                ? ss
                : new SegmentSummaryDto(key, 0, 0, Math.Round(capacity, 2), 0, false);

            return new SegmentComparisonDto(key, b, s);
        }).ToList();

        // Build response
        var baselineSummary = new BaselineSummaryDto(
            ExpectedKeptAppointments: Math.Round(baselineResult.ExpectedKept, 2),
            BaselineRevenue: Math.Round(baselineResult.RevenueRecovered, 2),
            BlockCapacityAppointments: Math.Round(capacity, 2),
            ExpectedOverflowAppointments: Math.Round(baselineOverflow, 2),
            IsOverCapacity: baselineIsOver
        );

        var simulatedSummary = new BaselineSummaryDto(
            ExpectedKeptAppointments: Math.Round(simulatedResult.ExpectedKept, 2),
            BaselineRevenue: Math.Round(simulatedResult.RevenueRecovered, 2),
            BlockCapacityAppointments: Math.Round(capacity, 2),
            ExpectedOverflowAppointments: Math.Round(simulatedOverflow, 2),
            IsOverCapacity: simulatedIsOver
        );

        var delta = new SummaryDeltaDto(
            DeltaExpectedKeptAppointments: Math.Round(simulatedResult.ExpectedKept - baselineResult.ExpectedKept, 2),
            DeltaRevenue: Math.Round(simulatedResult.RevenueRecovered - baselineResult.RevenueRecovered, 2),
            DeltaExpectedOverflowAppointments: Math.Round(simulatedOverflow - baselineOverflow, 2)
        );

        return new ScheduleSimulationResponseDto(
            StrategyName: req.Strategy.Name,
            BaselineSummary: baselineSummary,
            SimulatedSummary: simulatedSummary,
            Delta: delta,
            Segments: comparisons
        );
    }
}
