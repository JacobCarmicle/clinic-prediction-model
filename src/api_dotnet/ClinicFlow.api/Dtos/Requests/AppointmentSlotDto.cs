namespace ClinicFlow.Api.Dtos.Requests;
using System.Text.Json;
using System.Text.Json.Serialization;
// TODO: Implement AppointmentSlotDto
public record AppointmentSlotDto(
    int AppointmentId,
    string AppointmentType,
    string DayOfWeek,
    string TimeBlock,
    int LeadTimeDays,
    bool IsNewPatient,
    float HistoricalNoShowRate,
    float UnitRevenue
)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
