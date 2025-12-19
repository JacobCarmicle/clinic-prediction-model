namespace ClinicFlow.Api.Dtos.Requests;
using System.Text.Json;
using System.Text.Json.Serialization;
// TODO: Implement ScheduleRequestDto
public record ScheduleRequestDto(
    ClinicProfileDto ClinicProfile,
    IReadOnlyList<AppointmentSlotDto> Appointments
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init;}
}