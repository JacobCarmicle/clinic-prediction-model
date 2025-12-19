using ClinicFlow.Api.Native;
using ClinicFlow.Api.Dtos.Requests;
using System;
using System.Linq;

namespace ClinicFlow.Api.Services.Simulation;

/// <summary>
/// Service that wraps the C simulation engine via P/Invoke.
/// Converts between .NET DTOs and C structures.
/// </summary>
public class SimulationService : ISimulationService
{
    public SimulationResult RunSimulation(
        SimulationRequest request,
        SimulationConfig config)
    {
        // Convert .NET types to C structures
        var appts = request.Appointments.Select(a => new SimulationNative.Appt
        {
            p_no_show = a.NoShowProbability,
            unit_revenue = (float)a.UnitRevenue,
            service_minutes = (ushort)(a.ServiceMinutes ?? config.AvgServiceMinutes),
            segment_id = a.SegmentId
        }).ToArray();

        var simConfig = new SimulationNative.SimConfig
        {
            providers_per_block = (ushort)config.ProvidersPerBlock,
            avg_service_minutes = (ushort)config.AvgServiceMinutes,
            time_block_minutes = (ushort)config.TimeBlockMinutes,
            num_trials = config.NumTrials,
            random_seed = config.RandomSeed
        };

        var result = new SimulationNative.SimResult();
        var status = SimulationNative.RunSimulation(
            appts,
            (uint)appts.Length,
            ref simConfig,
            out result
        );

        if (status != SimulationNative.SimStatus.SIM_OK)
        {
            throw new InvalidOperationException(
                $"Simulation failed with status: {status}");
        }

        return new SimulationResult
        {
            ExpectedKept = result.expected_kept,
            OverloadProbability = result.overload_probability,
            ExpectedWaitMinutes = result.expected_wait_minutes,
            P95WaitMinutes = result.p95_wait_minutes,
            RevenueRecovered = result.revenue_recovered
        };
    }

    public SimulationResult[] RunSimulationBatch(
        SimulationRequest request,
        SimulationConfig config,
        ushort[][] strategyOverbookAdditions,
        ushort nSegments)
    {
        // Convert .NET types to C structures
        var appts = request.Appointments.Select(a => new SimulationNative.Appt
        {
            p_no_show = a.NoShowProbability,
            unit_revenue = (float)a.UnitRevenue,
            service_minutes = (ushort)(a.ServiceMinutes ?? config.AvgServiceMinutes),
            segment_id = a.SegmentId
        }).ToArray();

        var simConfig = new SimulationNative.SimConfig
        {
            providers_per_block = (ushort)config.ProvidersPerBlock,
            avg_service_minutes = (ushort)config.AvgServiceMinutes,
            time_block_minutes = (ushort)config.TimeBlockMinutes,
            num_trials = config.NumTrials,
            random_seed = config.RandomSeed
        };

        // Flatten strategy array: [strategy0_seg0, strategy0_seg1, ..., strategy1_seg0, ...]
        var flatStrategies = new ushort[strategyOverbookAdditions.Length * nSegments];
        for (int s = 0; s < strategyOverbookAdditions.Length; s++)
        {
            for (int seg = 0; seg < nSegments; seg++)
            {
                flatStrategies[s * nSegments + seg] = 
                    seg < strategyOverbookAdditions[s].Length 
                        ? strategyOverbookAdditions[s][seg] 
                        : (ushort)0;
            }
        }

        var results = new SimulationNative.SimResult[strategyOverbookAdditions.Length];
        var status = SimulationNative.RunSimulationBatch(
            appts,
            (uint)appts.Length,
            ref simConfig,
            flatStrategies,
            (uint)strategyOverbookAdditions.Length,
            nSegments,
            results
        );

        if (status != SimulationNative.SimStatus.SIM_OK)
        {
            throw new InvalidOperationException(
                $"Batch simulation failed with status: {status}");
        }

        return results.Select(r => new SimulationResult
        {
            ExpectedKept = r.expected_kept,
            OverloadProbability = r.overload_probability,
            ExpectedWaitMinutes = r.expected_wait_minutes,
            P95WaitMinutes = r.p95_wait_minutes,
            RevenueRecovered = r.revenue_recovered
        }).ToArray();
    }
}

// Supporting types

public interface ISimulationService
{
    SimulationResult RunSimulation(SimulationRequest request, SimulationConfig config);
    SimulationResult[] RunSimulationBatch(
        SimulationRequest request,
        SimulationConfig config,
        ushort[][] strategyOverbookAdditions,
        ushort nSegments);
}

public class SimulationRequest
{
    public required SimulationAppointment[] Appointments { get; set; }
}

public class SimulationAppointment
{
    public required float NoShowProbability { get; set; }
    public required double UnitRevenue { get; set; }
    public ushort? ServiceMinutes { get; set; }
    public required byte SegmentId { get; set; }
}

public class SimulationConfig
{
    public required int ProvidersPerBlock { get; set; }
    public required int AvgServiceMinutes { get; set; }
    public required int TimeBlockMinutes { get; set; }
    public uint NumTrials { get; set; } = 5000;
    public uint RandomSeed { get; set; } = 12345;
}

public class SimulationResult
{
    public float ExpectedKept { get; set; }
    public float OverloadProbability { get; set; }
    public float ExpectedWaitMinutes { get; set; }
    public float P95WaitMinutes { get; set; }
    public float RevenueRecovered { get; set; }
}

