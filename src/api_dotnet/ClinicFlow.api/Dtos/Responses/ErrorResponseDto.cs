namespace ClinicFlow.Api.Dtos.Responses;

// TODO: Implement ErrorResponseDto
public sealed record ErrorResponseDto(
    string Error,
    IReadOnlyList<FieldErrorDto> Details
);