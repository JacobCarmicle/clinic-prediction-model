using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ClinicFlow.Api.Tests.Integration;

/// <summary>
/// Integration tests for the simulation engine P/Invoke integration.
/// These tests verify that the C simulation engine is properly called via P/Invoke.
/// </summary>
public class SimulationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public SimulationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", content);
    }

    [Fact]
    public async Task Simulate_WithValidRequest_ReturnsMonteCarloResults()
    {
        // Arrange
        var request = new
        {
            clinicProfile = new
            {
                clinicType = "PrimaryCare",
                providersPerBlock = 2,
                avgServiceMinutes = 15,
                timeBlockMinutes = 240
            },
            appointments = new[]
            {
                new
                {
                    appointmentId = 1,
                    appointmentType = "FollowUp",
                    dayOfWeek = "Tuesday",
                    timeBlock = "Morning",
                    leadTimeDays = 14,
                    isNewPatient = false,
                    historicalNoShowRate = 0.20f,
                    unitRevenue = 120.0
                },
                new
                {
                    appointmentId = 2,
                    appointmentType = "FollowUp",
                    dayOfWeek = "Tuesday",
                    timeBlock = "Morning",
                    leadTimeDays = 7,
                    isNewPatient = false,
                    historicalNoShowRate = 0.10f,
                    unitRevenue = 100.0
                }
            },
            strategy = new
            {
                name = "Test Strategy",
                overbookAdditionsBySegment = new Dictionary<string, int>
                {
                    { "Tuesday-Morning", 5 }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/schedule/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        // Verify structure
        Assert.True(result.TryGetProperty("strategyName", out _));
        Assert.True(result.TryGetProperty("baselineSummary", out _));
        Assert.True(result.TryGetProperty("simulatedSummary", out _));
        Assert.True(result.TryGetProperty("delta", out _));
        Assert.True(result.TryGetProperty("segments", out _));

        // Verify baseline summary has expected fields
        var baseline = result.GetProperty("baselineSummary");
        Assert.True(baseline.TryGetProperty("expectedKeptAppointments", out _));
        Assert.True(baseline.TryGetProperty("baselineRevenue", out _));
        Assert.True(baseline.TryGetProperty("blockCapacityAppointments", out _));

        // Verify simulated summary has expected fields
        var simulated = result.GetProperty("simulatedSummary");
        Assert.True(simulated.TryGetProperty("expectedKeptAppointments", out _));
        Assert.True(simulated.TryGetProperty("baselineRevenue", out _));

        // Verify delta exists
        var delta = result.GetProperty("delta");
        Assert.True(delta.TryGetProperty("deltaExpectedKeptAppointments", out _));
        Assert.True(delta.TryGetProperty("deltaRevenue", out _));
    }

    [Fact]
    public async Task Simulate_WithOverbooking_ShowsIncreasedKeptAppointments()
    {
        // Arrange
        var request = new
        {
            clinicProfile = new
            {
                clinicType = "PrimaryCare",
                providersPerBlock = 2,
                avgServiceMinutes = 15,
                timeBlockMinutes = 240
            },
            appointments = new[]
            {
                new
                {
                    appointmentId = 1,
                    appointmentType = "FollowUp",
                    dayOfWeek = "Tuesday",
                    timeBlock = "Morning",
                    leadTimeDays = 14,
                    isNewPatient = false,
                    historicalNoShowRate = 0.20f,
                    unitRevenue = 120.0
                }
            },
            strategy = new
            {
                name = "Overbooking Test",
                overbookAdditionsBySegment = new Dictionary<string, int>
                {
                    { "Tuesday-Morning", 10 }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/schedule/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        var baselineKept = result.GetProperty("baselineSummary")
            .GetProperty("expectedKeptAppointments").GetSingle();
        var simulatedKept = result.GetProperty("simulatedSummary")
            .GetProperty("expectedKeptAppointments").GetSingle();
        var deltaKept = result.GetProperty("delta")
            .GetProperty("deltaExpectedKeptAppointments").GetSingle();

        // Simulated should be greater than baseline when overbooking
        Assert.True(simulatedKept > baselineKept, 
            $"Simulated kept ({simulatedKept}) should be greater than baseline ({baselineKept})");
        
        // Delta should be positive
        Assert.True(deltaKept > 0, 
            $"Delta should be positive, got {deltaKept}");
    }

    [Fact]
    public async Task Simulate_WithMultipleSegments_ReturnsSegmentBreakdown()
    {
        // Arrange
        var request = new
        {
            clinicProfile = new
            {
                clinicType = "PrimaryCare",
                providersPerBlock = 2,
                avgServiceMinutes = 15,
                timeBlockMinutes = 240
            },
            appointments = new[]
            {
                new
                {
                    appointmentId = 1,
                    appointmentType = "FollowUp",
                    dayOfWeek = "Tuesday",
                    timeBlock = "Morning",
                    leadTimeDays = 14,
                    isNewPatient = false,
                    historicalNoShowRate = 0.20f,
                    unitRevenue = 120.0
                },
                new
                {
                    appointmentId = 2,
                    appointmentType = "NewPatient",
                    dayOfWeek = "Friday",
                    timeBlock = "Afternoon",
                    leadTimeDays = 21,
                    isNewPatient = true,
                    historicalNoShowRate = 0.35f,
                    unitRevenue = 150.0
                }
            },
            strategy = new
            {
                name = "Multi-Segment Test",
                overbookAdditionsBySegment = new Dictionary<string, int>
                {
                    { "Tuesday-Morning", 3 },
                    { "Friday-Afternoon", 2 }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/schedule/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        var segments = result.GetProperty("segments");
        Assert.True(segments.GetArrayLength() >= 2, 
            "Should have at least 2 segments");

        // Verify each segment has baseline and simulated
        foreach (var segment in segments.EnumerateArray())
        {
            Assert.True(segment.TryGetProperty("segmentKey", out _));
            Assert.True(segment.TryGetProperty("baseline", out _));
            Assert.True(segment.TryGetProperty("simulated", out _));
        }
    }

    [Fact]
    public async Task Simulate_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange - missing required fields
        var request = new
        {
            clinicProfile = new
            {
                clinicType = "PrimaryCare"
                // Missing required fields
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/schedule/simulate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

