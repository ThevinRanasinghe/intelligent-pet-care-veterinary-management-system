using PetCare.Domain.Enums;

namespace PetCare.Application.DTOs.Scheduling;

public record AppointmentResponseDto(
    Guid Id,
    Guid? OrganizationId,
    Guid PetId,
    string PetName,
    string OwnerName,
    Guid VeterinarianId,
    string VeterinarianName,
    Guid AppointmentSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    string? Notes,
    decimal? QuotationTotal,
    DateTimeOffset CreatedAt
);

public record CreateAppointmentDto(
    Guid PetId,
    Guid VeterinarianId,
    Guid AppointmentSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes
);

public record UpdateAppointmentDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes
);

public record UpdateAppointmentStatusDto(
    AppointmentStatus Status
);

public record AppointmentSlotResponseDto(
    Guid Id,
    Guid VeterinarianId,
    string VeterinarianName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Branch,
    string Status,
    string? PetName,
    string? OwnerName
);

public record CreateAppointmentSlotDto(
    Guid VeterinarianId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Branch
);

public record UpdateAppointmentSlotDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Branch,
    AppointmentSlotStatus? Status
);

public record ConflictCheckDto(
    Guid VeterinarianId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ExcludeAppointmentId = null
);

public record VeterinarianDto(
    Guid Id,
    Guid? OrganizationId,
    string Name,
    string Specialisation,
    string Branch,
    bool Active
);
