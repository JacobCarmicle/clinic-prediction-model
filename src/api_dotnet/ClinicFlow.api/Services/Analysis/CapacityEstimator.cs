namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;
using System;

public static class CapacityEstimator
{
    ///<summary>
    /// Calculates capacity in "appointments" for a single time block using clinic profile assumptions.
    /// </summary>
    public static double ComputeBlockCapacityAppointments(ClinicProfileDto profile)
    {
        //Capacity = providers * minutes available / minutes per appointment
        //Example: 2 providers * 240 minutes / 15 min = 32 appointments
        return (profile.ProvidersPerBlock * (double)profile.TimeBlockMinutes) / profile.AvgServiceMinutes;
    }

    public static (double overflow, bool isOverCapacity) ComputeOverflow(double expectedKept, double capacity)
    {
        var overflow = Math.Max(0.0, expectedKept - capacity);
        return (overflow, overflow > 0.000001);
    }
}