using System.Globalization;
using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;

namespace ClinicFlow.Api.Validation;
// TODO: Implement ScheduleRequestValidator

public static class ScheduleRequestValidator{
    private static readonly HashSet<string> AllowedClinicTypes = new(StringComparer.OrdinalIgnoreCase){
        "PrimaryCare", "Dental", "PT", "Behavioral", "Other"
    };

    private static readonly HashSet<string> AllowedAppointmentTypes = new(StringComparer.OrdinalIgnoreCase){
        "NewPatient", "FollowUp", "Procedure", "Lab", "Other"
    };

    private static readonly HashSet<string> AllowedDays = new(StringComparer.OrdinalIgnoreCase){
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    private static readonly HashSet<string> AllowedTimeBlocks = new(StringComparer.OrdinalIgnoreCase){
        "Morning", "Midday", "Afternoon", "Evening"
    };

    public static List<FieldErrorDto> Validate(ScheduleRequestDto? req){
        var errors = new List<FieldErrorDto>();

        if(req is null){
            errors.Add(new FieldErrorDto("$", "Request body is required"));
            return errors;
        }

        RejectUnknownFields(req.ExtraFields, "$", errors);
        if(req.ClinicProfile is null){
            errors.Add(new FieldErrorDto("clinicProfile", "clinicProfiles is required."));

        }
        else{
            RejectUnknownFields(req.ClinicProfile.ExtraFields, "clinicProfile", errors);

            if(string.IsNullOrWhiteSpace(req.ClinicProfile.ClinicType))
                errors.Add(new FieldErrorDto("clinicProfile.clinictype", "clinictype is required."));
            else if (!AllowedClinicTypes.Contains(req.ClinicProfile.ClinicType))
                errors.Add(new FieldErrorDto("clinicProfile.clinicType", $"Invalid clinicType. Allowed: {string.Join(", ", AllowedClinicTypes)}"));

            if (req.ClinicProfile.ProvidersPerBlock <= 0)
                errors.Add(new FieldErrorDto("clinicProfile.providersPerBlock", "providersPerBlock must be >= 1."));

            if (req.ClinicProfile.AvgServiceMinutes <= 0)
                errors.Add(new FieldErrorDto("clinicProfile.avgServiceMinutes", "avgServiceMinutes must be >= 1."));

            if (req.ClinicProfile.TimeBlockMinutes <= 0)
                errors.Add(new FieldErrorDto("clinicProfile.timeBlockMinutes", "timeBlockMinutes must be >= 1."));
        }

        if (req.Appointments is null || req.Appointments.Count == 0)
        {
            errors.Add(new FieldErrorDto("appointments", "appointments must contain at least 1 item."));
            return errors;
        }

        // demo-safe cap (adjust later)
        if (req.Appointments.Count > 200)
            errors.Add(new FieldErrorDto("appointments", "appointments exceeds demo limit (200)."));

        for (int i = 0; i < req.Appointments.Count; i++)
        {
            var a = req.Appointments[i];
            var prefix = $"appointments[{i}]";

            RejectUnknownFields(a.ExtraFields, prefix, errors);

            if (a.AppointmentId < 0)
                errors.Add(new FieldErrorDto($"{prefix}.appointmentId", "appointmentId must be >= 0."));

            if (string.IsNullOrWhiteSpace(a.AppointmentType))
                errors.Add(new FieldErrorDto($"{prefix}.appointmentType", "appointmentType is required."));
            else if (!AllowedAppointmentTypes.Contains(a.AppointmentType))
                errors.Add(new FieldErrorDto($"{prefix}.appointmentType", $"Invalid appointmentType. Allowed: {string.Join(", ", AllowedAppointmentTypes)}"));

            if (string.IsNullOrWhiteSpace(a.DayOfWeek))
                errors.Add(new FieldErrorDto($"{prefix}.dayOfWeek", "dayOfWeek is required."));
            else if (!AllowedDays.Contains(a.DayOfWeek))
                errors.Add(new FieldErrorDto($"{prefix}.dayOfWeek", $"Invalid dayOfWeek. Allowed: {string.Join(", ", AllowedDays)}"));

            if (string.IsNullOrWhiteSpace(a.TimeBlock))
                errors.Add(new FieldErrorDto($"{prefix}.timeBlock", "timeBlock is required."));
            else if (!AllowedTimeBlocks.Contains(a.TimeBlock))
                errors.Add(new FieldErrorDto($"{prefix}.timeBlock", $"Invalid timeBlock. Allowed: {string.Join(", ", AllowedTimeBlocks)}"));

            if (a.LeadTimeDays < 0)
                errors.Add(new FieldErrorDto($"{prefix}.leadTimeDays", "leadTimeDays must be >= 0."));

            if (a.HistoricalNoShowRate < 0 || a.HistoricalNoShowRate > 1)
                errors.Add(new FieldErrorDto($"{prefix}.historicalNoShowRate", "historicalNoShowRate must be between 0 and 1."));

            if (a.UnitRevenue < 0)
                errors.Add(new FieldErrorDto($"{prefix}.unitRevenue", "unitRevenue must be >= 0."));
        }

        return errors;
    }

    private static void RejectUnknownFields(Dictionary<string, System.Text.Json.JsonElement>? extra, string path, List<FieldErrorDto> errors)
    {
        if (extra is null || extra.Count == 0) return;

        // This is the HIPAA-safety guardrail: unknown fields are rejected
        foreach (var key in extra.Keys)
        {
            errors.Add(new FieldErrorDto($"{path}.{key}", "Unknown field is not allowed. Remove this field and try again."));
        }
    }

    public static List<FieldErrorDto> ValidateSimulate(ScheduleSimulateRequestDto? req)
    {
        var errors = new List<FieldErrorDto>();

        if (req is null)
        {
            errors.Add(new FieldErrorDto("$", "Request body is required"));
            return errors;
        }
        RejectUnknownFields(req.ExtraFields, "$", errors);

        //Reuse existing validation mapping to ScheduleRequestDto shape
        var core = new ScheduleRequestDto(req.ClinicProfile, req.Appointments);
        errors.AddRange(Validate(core));

        if (req.Strategy is null)
        {
            errors.Add(new FieldErrorDto("strategy", "strategy is required."));
            return errors;
        }
        RejectUnknownFields(req.Strategy.ExtraFields, "strategy", errors);

        if (string.IsNullOrWhiteSpace(req.Strategy.Name))
            errors.Add(new FieldErrorDto("strategy.name", "strategy.name is required."));

        if (req.Strategy.OverbookAdditionsBySegment is null)
        {
            errors.Add(new FieldErrorDto("strategy.overbookAdditionsBySegment", "strategy.overbookAdditionsBySegment is required."));
            return errors;
        }

        foreach (var kvp in req.Strategy.OverbookAdditionsBySegment)
        {
            var seg = kvp.Key;
            var add = kvp.Value;

            if (string.IsNullOrWhiteSpace(seg))
                errors.Add(new FieldErrorDto("strategy.overbookAdditionsBySegment", "segment key is required."));

            if (add < 0)
                errors.Add(new FieldErrorDto("strategy.overbookAdditionsBySegment", "overbook addition must be >= 0."));
            //Demo Guardrail
            if (add > 25)
                errors.Add(new FieldErrorDto($"strategy.overbookAdditionsBySegment.{seg}", "Addition exceeds demo max."));
        }
        return errors;
    }

    public static List<FieldErrorDto> ValidateOptimize(ScheduleOptimizeRequestDto? req)
    {
        var errors = new List<FieldErrorDto>();

        if (req is null)
        {
            errors.Add(new FieldErrorDto("$", "Request body is required."));
            return errors;
        }

        RejectUnknownFields(req.ExtraFields, "$", errors);

        // Validate core schedule via existing validator
        var core = new ScheduleRequestDto(req.ClinicProfile, req.Appointments);
        errors.AddRange(Validate(core));

        if (req.Constraints is null)
        {
            errors.Add(new FieldErrorDto("constraints", "constraints is required."));
            return errors;
        }
        RejectUnknownFields(req.Constraints.ExtraFields, "constraints", errors);

        if (req.Constraints.MaxExpectedOverflowAppointments < 0)
            errors.Add(new FieldErrorDto("constraints.maxExpectedOverflowAppointments", "Must be >= 0."));

        if (req.Search is null)
        {
            errors.Add(new FieldErrorDto("search", "search is required."));
            return errors;
        }
        RejectUnknownFields(req.Search.ExtraFields, "search", errors);

        if (req.Search.MaxAdditionPerSegment < 0 || req.Search.MaxAdditionPerSegment > 25)
            errors.Add(new FieldErrorDto("search.maxAdditionPerSegment", "Must be between 0 and 25."));

        if (req.Search.MaxTotalAdditions < 0 || req.Search.MaxTotalAdditions > 50)
            errors.Add(new FieldErrorDto("search.maxTotalAdditions", "Must be between 0 and 50."));

        return errors;
    }
}