namespace ClinicFlow.Api.Dtos.Requests;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record StrategyDto(
    string Name,
    Dictionary<string, int> OverbookAdditionsBySegment
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}