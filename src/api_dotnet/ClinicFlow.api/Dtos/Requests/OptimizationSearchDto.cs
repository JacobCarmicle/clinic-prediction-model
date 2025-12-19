using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicFlow.Api.Dtos.Requests;

public sealed record OptimizationSearchDto(
    int MaxAdditionPerSegment,
    int MaxTotalAdditions,
    IReadOnlyList<string>? AllowedSegments
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
