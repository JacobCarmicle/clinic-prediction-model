namespace ClinicFlow.Api.Dtos.Responses;

public sealed record FieldErrorDto(
    string Field,
    string Message
);
