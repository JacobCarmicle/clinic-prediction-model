namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using System.Linq;

public static class BaselineMetricsCalculator
{
    /// <summary>
    /// Compute metrics using historical no-show rates (baseline method).
    /// </summary>
    public static (double expectedKept, double baselineRevenue) Compute(ScheduleRequestDto req){
        double expectedKept = 0.0;
        double baselineRevenue = 0.0;

        foreach(var a in req.Appointments){
            //Expected show probability based on historical no-show rate
            var pShow = 1.0 - Clamp01(a.HistoricalNoShowRate);

            expectedKept += pShow;
            baselineRevenue += pShow * a.UnitRevenue;
        }
        return (expectedKept, baselineRevenue);
    }

    /// <summary>
    /// Compute metrics using ML predictions.
    /// </summary>
    public static (double expectedKept, double baselineRevenue) ComputeWithPredictions(
        ScheduleRequestDto req,
        IReadOnlyList<SlotPredictionStubDto> predictions)
    {
        double expectedKept = 0.0;
        double baselineRevenue = 0.0;

        // Create lookup dictionary by appointment ID
        var predLookup = predictions.ToDictionary(p => p.AppointmentId, p => p.NoShowProbability);

        for (int i = 0; i < req.Appointments.Count; i++)
        {
            var appointment = req.Appointments[i];
            var pNoShow = predLookup.GetValueOrDefault(appointment.AppointmentId, appointment.HistoricalNoShowRate);
            var pShow = 1.0 - Clamp01(pNoShow);

            expectedKept += pShow;
            baselineRevenue += pShow * appointment.UnitRevenue;
        }

        return (expectedKept, baselineRevenue);
    }
    
    private static double Clamp01(double x){
        if(x < 0.0) return 0.0;
        if(x > 1.0) return 1.0;
        return x;
    }
}