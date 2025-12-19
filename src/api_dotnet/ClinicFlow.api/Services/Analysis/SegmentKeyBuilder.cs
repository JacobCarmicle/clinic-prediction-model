namespace ClinicFlow.Api.Services.Analysis;

using ClinicFlow.Api.Dtos.Requests;

public static class SegmentKeyBuilder
{
    public static string Build(AppointmentSlotDto a)
        => $"{a.DayOfWeek}-{a.TimeBlock}";
}