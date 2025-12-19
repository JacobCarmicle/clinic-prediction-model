using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicFlow.Api.Dtos.Requests;

public sealed record OptimizationConstraintsDto(
    double MaxExpectedOverflowAppointments,
    double? MinDeltaRevenue
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
