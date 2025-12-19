using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicFlow.Api.Dtos.Requests;

public sealed record ScheduleOptimizeRequestDto(
    ClinicProfileDto ClinicProfile,
    IReadOnlyList<AppointmentSlotDto> Appointments,
    OptimizationConstraintsDto Constraints,
    OptimizationSearchDto Search
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
