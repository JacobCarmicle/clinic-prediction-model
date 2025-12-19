namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;

public static class BaselineMetricsCalculator
{
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
    
    private static double Clamp01(float x){
        if(x < 0f) return 0.0;
        if(x > 1f) return 1.0;
        return x;
    }
}