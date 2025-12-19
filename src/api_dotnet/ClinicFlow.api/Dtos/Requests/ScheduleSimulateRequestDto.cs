namespace ClinicFlow.Api.Dtos.Requests;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record ScheduleSimulateRequestDto(
    ClinicProfileDto ClinicProfile,
    IReadOnlyList<AppointmentSlotDto> Appointments,
    StrategyDto Strategy
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}