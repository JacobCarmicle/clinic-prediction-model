using System.Net.Http.Json;
using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ClinicFlow.Api.Services.Inference;

public class InferenceService : IInferenceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InferenceService> _logger;
    private readonly string _mlServiceUrl;

    public InferenceService(
        HttpClient httpClient,
        ILogger<InferenceService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _mlServiceUrl = configuration["MLService:BaseUrl"] ?? "http://localhost:8000";
        
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_mlServiceUrl);
        }
        
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<SlotPredictionStubDto>> PredictAsync(
        ScheduleRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            // Convert .NET DTOs to Python service format
            var payload = new
            {
                clinic_profile = new
                {
                    clinic_type = request.ClinicProfile.ClinicType,
                    providers_per_block = request.ClinicProfile.ProvidersPerBlock,
                    avg_service_minutes = request.ClinicProfile.AvgServiceMinutes,
                    time_block_minutes = request.ClinicProfile.TimeBlockMinutes
                },
                appointments = request.Appointments.Select(a => new
                {
                    appointment_type = a.AppointmentType,
                    day_of_week = a.DayOfWeek,
                    time_block = a.TimeBlock,
                    lead_time_days = a.LeadTimeDays,
                    is_new_patient = a.IsNewPatient,
                    historical_no_show_rate = a.HistoricalNoShowRate,
                    unit_revenue = a.UnitRevenue,
                    appointment_id = a.AppointmentId
                }).ToList(),
                explain = true
            };

            _logger.LogDebug("Calling ML service at {Url}/predict", _mlServiceUrl);

            var response = await _httpClient.PostAsJsonAsync("/predict", payload, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "ML service returned {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);
                
                // Fallback to historical rates
                return FallbackToHistoricalRates(request);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonPredictionResponse>(
                cancellationToken: ct);

            if (result?.Predictions == null || result.Predictions.Count != request.Appointments.Count)
            {
                _logger.LogWarning(
                    "ML service returned mismatched prediction count. Expected {Expected}, got {Actual}",
                    request.Appointments.Count,
                    result?.Predictions?.Count ?? 0);
                
                return FallbackToHistoricalRates(request);
            }

            // Map Python service predictions back to DTOs
            var predictions = new List<SlotPredictionStubDto>();
            for (int i = 0; i < request.Appointments.Count; i++)
            {
                var appointment = request.Appointments[i];
                var pythonPred = result.Predictions[i];
                
                predictions.Add(new SlotPredictionStubDto(
                    AppointmentId: appointment.AppointmentId,
                    NoShowProbability: pythonPred.NoShowProbability
                ));
            }

            _logger.LogDebug(
                "ML service returned {Count} predictions with mean probability {MeanProb:F4}",
                predictions.Count,
                predictions.Average(p => p.NoShowProbability));

            return predictions;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML inference service unavailable at {Url}", _mlServiceUrl);
            return FallbackToHistoricalRates(request);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "ML inference service timeout");
            return FallbackToHistoricalRates(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling ML inference service");
            return FallbackToHistoricalRates(request);
        }
    }

    private List<SlotPredictionStubDto> FallbackToHistoricalRates(ScheduleRequestDto request)
    {
        _logger.LogWarning(
            "Falling back to historical no-show rates for {Count} appointments",
            request.Appointments.Count);
        
        return request.Appointments
            .Select(a => new SlotPredictionStubDto(
                a.AppointmentId,
                a.HistoricalNoShowRate
            ))
            .ToList();
    }
}

// Internal DTOs for Python service response
// Note: Using JsonPropertyName to handle snake_case from Python service
internal record PythonPredictionResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("predictions")]
    public List<PythonPrediction>? Predictions { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("schema_hash")]
    public string? SchemaHash { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("warnings")]
    public List<string>? Warnings { get; init; }
}

internal record PythonPrediction
{
    [System.Text.Json.Serialization.JsonPropertyName("no_show_probability")]
    public double NoShowProbability { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("confidence_interval")]
    public double[]? ConfidenceInterval { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("top_drivers")]
    public List<string>? TopDrivers { get; init; }
}

