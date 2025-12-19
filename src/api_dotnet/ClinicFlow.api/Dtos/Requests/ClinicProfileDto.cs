namespace ClinicFlow.Api.Dtos.Requests;
using System.Text.Json;
using System.Text.Json.Serialization;
// TODO: Implement ClinicProfileDto

public record ClinicProfileDto(
    string ClinicType,
    int ProvidersPerBlock,
    int AvgServiceMinutes,
    int TimeBlockMinutes
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
