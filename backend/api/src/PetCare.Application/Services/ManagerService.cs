using System.Security.Cryptography;
using FluentValidation;
using PetCare.Application.DTOs.Manager;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

public class ManagerService : IManagerService
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenant;
    private readonly IValidator<ManagerCreateStaffRequest> _createStaffValidator;
    private readonly IVeterinarianRepository _veterinarians;
    private readonly IAppointmentRepository _appointments;
    private readonly IExaminationRepository _examinations;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IQuotationRepository _quotations;

    public ManagerService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IPasswordHasher passwordHasher,
        ITenantContext tenant,
        IValidator<ManagerCreateStaffRequest> createStaffValidator,
        IVeterinarianRepository veterinarians,
        IAppointmentRepository appointments,
        IExaminationRepository examinations,
        IPrescriptionRepository prescriptions,
        IQuotationRepository quotations)
    {
        _users = users;
        _organizations = organizations;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
        _createStaffValidator = createStaffValidator;
        _veterinarians = veterinarians;
        _appointments = appointments;
        _examinations = examinations;
        _prescriptions = prescriptions;
        _quotations = quotations;
    }

    public Task<CreatedStaffAccountResponse> CreateVeterinarianAsync(
        ManagerCreateStaffRequest request, CancellationToken cancellationToken = default)
        => CreateStaffAccountAsync(request, Roles.Veterinarian, cancellationToken);

    public Task<CreatedStaffAccountResponse> CreateInventoryOfficerAsync(
        ManagerCreateStaffRequest request, CancellationToken cancellationToken = default)
        => CreateStaffAccountAsync(request, Roles.InventoryOfficer, cancellationToken);

    /// <summary>
    /// Shared staff-creation path — the role is fixed by the calling
    /// endpoint and the organization comes exclusively from the
    /// authenticated ClinicManager's tenant context, never from the
    /// request. A manager can therefore never place a staff account in an
    /// organization they do not belong to.
    /// </summary>
    private async Task<CreatedStaffAccountResponse> CreateStaffAccountAsync(
        ManagerCreateStaffRequest request, string role, CancellationToken cancellationToken)
    {
        await _createStaffValidator.ValidateAndThrowAsync(request, cancellationToken);

        var organizationId = await _tenant.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null)
        {
            throw new ForbiddenException(
                "Your account is not linked to an organization, so staff accounts cannot be created.");
        }

        var organization = await _organizations.GetByIdAsync(organizationId.Value, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        if (organization.Status != OrganizationStatus.Active)
        {
            throw new ForbiddenException(
                "Staff accounts can only be created while your organization is Active.");
        }

        var email = request.Email.Trim();
        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ArgumentException("An account with this email already exists.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(temporaryPassword),
            Name = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = role,
            Active = true,
            MustChangePassword = true,
            OrganizationId = organization.Id,
        };

        await _users.AddAsync(user, cancellationToken);

        if (role == Roles.Veterinarian)
        {
            // Veterinarian logins need a scheduling profile (slots,
            // appointments, examinations) linked via Veterinarian.UserId;
            // create it in the same unit of work so the two never diverge.
            await _veterinarians.AddAsync(new Veterinarian
            {
                Name = user.Name,
                Specialisation = "General",
                Branch = organization.Name,
                Active = true,
                OrganizationId = organization.Id,
                UserId = user.Id
            }, cancellationToken);
        }

        await _users.SaveChangesAsync(cancellationToken);

        return new CreatedStaffAccountResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Active = user.Active,
            MustChangePassword = user.MustChangePassword,
            OrganizationId = user.OrganizationId,
            OrganizationName = organization.Name,
            CreatedAt = user.CreatedAt,
            TemporaryPassword = temporaryPassword,
        };
    }

    public async Task<IReadOnlyList<ManagerVeterinarianResponse>> GetVeterinariansAsync(
        CancellationToken cancellationToken = default)
    {
        var organizationId = await _tenant.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null)
        {
            throw new ForbiddenException(
                "Your account is not linked to an organization.");
        }

        var veterinarians = await _veterinarians.GetActiveByOrganizationAsync(organizationId.Value, cancellationToken);

        return veterinarians.Select(v => new ManagerVeterinarianResponse
        {
            Id = v.Id,
            Name = v.Name,
            Specialisation = v.Specialisation,
            Branch = v.Branch,
            Active = v.Active,
            UserId = v.UserId
        }).ToList();
    }

    public async Task<VeterinarianHistoryResponse> GetVeterinarianHistoryAsync(
        Guid veterinarianId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        // Org-scoped: a veterinarian outside the caller's organization is
        // indistinguishable from a missing one.
        var veterinarian = await _veterinarians.GetByIdAsync(veterinarianId, cancellationToken)
            ?? throw new NotFoundException($"Veterinarian '{veterinarianId}' does not exist.");

        var appointments = (await _appointments.GetByVeterinarianAsync(veterinarianId, cancellationToken))
            .Where(a => InRange(a.Date, from, to))
            .ToList();

        var examinations = (await _examinations.GetByVeterinarianAsync(veterinarianId, cancellationToken))
            .Where(e => InRange(DateOnly.FromDateTime(e.ExaminationDate), from, to))
            .ToList();

        var prescriptions = (await _prescriptions.GetByVeterinarianAsync(veterinarianId, cancellationToken))
            .Where(p => InRange(DateOnly.FromDateTime(p.CreatedAt), from, to))
            .ToList();

        var bills = (await _quotations.GetByVeterinarianAsync(veterinarianId, cancellationToken))
            .Where(q => InRange(DateOnly.FromDateTime(q.CreatedAt.DateTime), from, to))
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var billResponses = bills.Select(DTOs.Billing.QuotationMapper.ToResponse).ToList();

        return new VeterinarianHistoryResponse
        {
            Veterinarian = new VeterinarianHistoryVet
            {
                Id = veterinarian.Id,
                Name = veterinarian.Name,
                Specialisation = veterinarian.Specialisation
            },
            Appointments = new VeterinarianHistoryAppointments
            {
                CompletedCount = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                UpcomingCount = appointments.Count(a =>
                    a.Date >= today
                    && a.Status != AppointmentStatus.Completed
                    && a.Status != AppointmentStatus.Cancelled),
                CancelledCount = appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
                Items = appointments.Select(DTOs.Scheduling.AppointmentMapper.ToResponse).ToList()
            },
            Examinations = new VeterinarianHistoryExaminations
            {
                Total = examinations.Count,
                InitialCount = examinations.Count(e => e.Appointment?.Type != AppointmentType.FollowUp),
                FollowUpCount = examinations.Count(e => e.Appointment?.Type == AppointmentType.FollowUp),
                Items = examinations.Select(DTOs.ExaminationMapper.ToDto).ToList()
            },
            Prescriptions = new VeterinarianHistoryPrescriptions
            {
                Total = prescriptions.Count,
                Items = prescriptions.Select(DTOs.PrescriptionMapper.ToDto).ToList()
            },
            MedicineRequests = new VeterinarianHistoryMedicineRequests
            {
                Pending = prescriptions.Count(p => p.RequestStatus == MedicineRequestStatus.Pending),
                Issued = prescriptions.Count(p => p.RequestStatus == MedicineRequestStatus.Issued),
                Unavailable = prescriptions.Count(p => p.RequestStatus == MedicineRequestStatus.Unavailable)
            },
            Bills = new VeterinarianHistoryBills
            {
                Total = bills.Count,
                PaidCount = bills.Count(q => q.PaymentStatus == PaymentStatus.Paid),
                PendingCount = bills.Count(q => q.PaymentStatus == PaymentStatus.Pending),
                VetChargeTotal = billResponses.Sum(q => q.VeterinarianChargeTotal),
                MedicineTotal = billResponses.Sum(q => q.MedicineTotal),
                GrandTotal = bills.Sum(q => q.Total),
                Items = billResponses
            }
        };
    }

    private static bool InRange(DateOnly date, DateOnly? from, DateOnly? to) =>
        (from is null || date >= from.Value) && (to is null || date <= to.Value);

    /// <summary>
    /// Cryptographically random temporary password satisfying the platform's
    /// complexity policy (upper/lower/digit/special, 16+ chars).
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var chars = new char[16];
        for (var i = 0; i < chars.Length - 1; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Guarantee at least one of each required class.
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[^1] = '!';

        return new string(chars);
    }
}
