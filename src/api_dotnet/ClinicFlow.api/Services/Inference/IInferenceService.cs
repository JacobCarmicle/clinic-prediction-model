namespace ClinicFlow.Api.Services.Inference;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;

public interface IInferenceService
{
    /// <summary>
    /// Predict no-show probabilities for all appointments in the schedule.
    /// </summary>
    /// <param name="request">Schedule request with appointments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of predictions, one per appointment, ordered to match request.Appointments</returns>
    Task<List<SlotPredictionStubDto>> PredictAsync(
        ScheduleRequestDto request,
        CancellationToken ct = default);
}
