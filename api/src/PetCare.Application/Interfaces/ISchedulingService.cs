using PetCare.Application.DTOs.Scheduling;
using PetCare.Domain.Enums;

namespace PetCare.Application.Interfaces;

public interface ISchedulingService
{
    Task<IReadOnlyList<AppointmentResponseDto>> GetAppointmentsAsync(
        Guid organizationId,
        string? search = null,
        AppointmentStatus? status = null,
        DateOnly? date = null,
        CancellationToken ct = default);

    Task<AppointmentResponseDto?> GetAppointmentByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<AppointmentResponseDto> CreateAppointmentAsync(
        Guid organizationId,
        CreateAppointmentDto request,
        CancellationToken ct = default);

    Task<AppointmentResponseDto> UpdateAppointmentAsync(
        Guid id,
        Guid organizationId,
        UpdateAppointmentDto request,
        CancellationToken ct = default);

    Task<AppointmentResponseDto> UpdateAppointmentStatusAsync(
        Guid id,
        Guid organizationId,
        AppointmentStatus newStatus,
        CancellationToken ct = default);

    Task CancelAppointmentAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AppointmentSlotResponseDto>> GetSlotsAsync(
        Guid organizationId,
        Guid? veterinarianId = null,
        DateOnly? date = null,
        CancellationToken ct = default);

    Task<AppointmentSlotResponseDto> CreateSlotAsync(
        Guid organizationId,
        CreateAppointmentSlotDto request,
        CancellationToken ct = default);

    Task<AppointmentSlotResponseDto> UpdateSlotAsync(
        Guid id,
        Guid organizationId,
        UpdateAppointmentSlotDto request,
        CancellationToken ct = default);

    Task DeleteSlotAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AppointmentSlotResponseDto>> GetAvailableSlotsAsync(
        Guid organizationId,
        Guid? veterinarianId = null,
        DateOnly? date = null,
        CancellationToken ct = default);

    Task<bool> CheckConflictAsync(
        Guid organizationId,
        ConflictCheckDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<VeterinarianDto>> GetOrganizationVeterinariansAsync(
        Guid organizationId,
        CancellationToken ct = default);
}
