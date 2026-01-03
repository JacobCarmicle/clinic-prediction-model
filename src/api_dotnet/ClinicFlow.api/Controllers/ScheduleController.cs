namespace ClinicFlow.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;
using ClinicFlow.Api.Validation;
using ClinicFlow.Api.Services.Analysis;
using ClinicFlow.Api.Services.Optimization;
using System.Linq;
using System;
using ClinicFlow.Api.Services.Inference;

[ApiController]
[Route("schedule")]

public class ScheduleController: ControllerBase{
    private readonly Services.Inference.IInferenceService _inferenceService;

    public ScheduleController(Services.Inference.IInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] ScheduleRequestDto request)
    {
        var errors = ScheduleRequestValidator.Validate(request);
        if(errors.Count > 0){
            return BadRequest(new ErrorResponseDto("ValidationFailed", errors));
        }

        // Get ML predictions (falls back to historical rates if ML unavailable)
        var slotPreds = await _inferenceService.PredictAsync(request);

        // Compute metrics using ML predictions
        var (expectedKept, baselineRevenue) = BaselineMetricsCalculator.ComputeWithPredictions(request, slotPreds);

        //Calculate capacity metrics
        var blockCapacity = CapacityEstimator.ComputeBlockCapacityAppointments(request.ClinicProfile);
        var (overflow, isOverCapacity) = CapacityEstimator.ComputeOverflow(expectedKept, blockCapacity);

        //Calculate segment-level metrics using ML predictions
        var segments = SegmentMetricsCalculator.ComputeBySegmentWithPredictions(request, blockCapacity, slotPreds);
    
        var response = new ScheduleAnalysisResponseDto(
            new BaselineSummaryDto(
                ExpectedKeptAppointments: Math.Round(expectedKept, 2),
                BaselineRevenue: Math.Round(baselineRevenue, 2),
                BlockCapacityAppointments: Math.Round(blockCapacity, 2),
                ExpectedOverflowAppointments: Math.Round(overflow, 2),
                IsOverCapacity: isOverCapacity
            ),
            slotPreds,
            segments
        );
        return Ok(response);
    }

    [HttpPost("simulate")]
    public IActionResult Simulate([FromBody] ScheduleSimulateRequestDto request)
    {
        var errors = ScheduleRequestValidator.ValidateSimulate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ErrorResponseDto("ValidationFailed", errors));
        }

        var response = StrategySimulationService.Simulate(request);
        return Ok(response);
    }

    [HttpPost("optimize")]
    public IActionResult Optimize([FromBody] ScheduleOptimizeRequestDto request)
    {
        var errors = ScheduleRequestValidator.ValidateOptimize(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ErrorResponseDto("ValidationFailed", errors));
        }

        var response = DeterministicOptimizer.Optimize(request);
        return Ok(response);
    }
}