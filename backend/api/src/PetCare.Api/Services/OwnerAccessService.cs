using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Infrastructure;

namespace PetCare.Api.Services;

/// <summary>
/// Resolves the PetOwner profile linked to the authenticated caller via the
/// JWT email claim, then verifies ownership of pets and the clinical graph
/// that hangs off them (consultation requests, examinations, diagnoses,
/// treatment records, prescriptions).
/// </summary>
public class OwnerAccessService : IOwnerAccessService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PetCareDbContext _db;

    private bool _ownerIdResolved;
    private string? _ownerId;

    public OwnerAccessService(IHttpContextAccessor httpContextAccessor, PetCareDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public bool IsPetOwner =>
        _httpContextAccessor.HttpContext?.User.IsInRole(Roles.PetOwner) == true;

    public string? CallerEmail =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public async Task<string?> GetOwnerIdAsync(CancellationToken cancellationToken = default)
    {
        if (!IsPetOwner)
        {
            return null;
        }

        if (_ownerIdResolved)
        {
            return _ownerId;
        }

        var email = CallerEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            _ownerIdResolved = true;
            return null;
        }

        _ownerId = await _db.PetOwners
            .AsNoTracking()
            .Where(owner => owner.Email == email)
            .Select(owner => owner.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _ownerIdResolved = true;
        return _ownerId;
    }

    public async Task<bool> OwnsPetAsync(string petId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.Pets
            .AsNoTracking()
            .AnyAsync(pet => pet.Id == petId && pet.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> OwnsConsultationAsync(string consultationId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.ConsultationRequests
            .AsNoTracking()
            .AnyAsync(request => request.Id == consultationId && request.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> OwnsExaminationAsync(Guid examinationId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.Examinations
            .AsNoTracking()
            .Where(examination => examination.Id == examinationId)
            .Join(_db.Pets, examination => examination.PetId, pet => pet.Id, (_, pet) => pet)
            .AnyAsync(pet => pet.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> OwnsDiagnosisAsync(Guid diagnosisId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.Diagnoses
            .AsNoTracking()
            .Where(diagnosis => diagnosis.Id == diagnosisId)
            .Join(_db.Examinations, diagnosis => diagnosis.ExaminationId, examination => examination.Id, (_, examination) => examination)
            .Join(_db.Pets, examination => examination.PetId, pet => pet.Id, (_, pet) => pet)
            .AnyAsync(pet => pet.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> OwnsTreatmentRecordAsync(Guid treatmentRecordId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.TreatmentRecords
            .AsNoTracking()
            .Where(record => record.Id == treatmentRecordId)
            .Join(_db.Diagnoses, record => record.DiagnosisId, diagnosis => diagnosis.Id, (_, diagnosis) => diagnosis)
            .Join(_db.Examinations, diagnosis => diagnosis.ExaminationId, examination => examination.Id, (_, examination) => examination)
            .Join(_db.Pets, examination => examination.PetId, pet => pet.Id, (_, pet) => pet)
            .AnyAsync(pet => pet.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> OwnsPrescriptionAsync(Guid prescriptionId, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync(cancellationToken);
        if (ownerId == null)
        {
            return false;
        }

        return await _db.Prescriptions
            .AsNoTracking()
            .Where(prescription => prescription.Id == prescriptionId)
            .Join(_db.TreatmentRecords, prescription => prescription.TreatmentRecordId, record => record.Id, (_, record) => record)
            .Join(_db.Diagnoses, record => record.DiagnosisId, diagnosis => diagnosis.Id, (_, diagnosis) => diagnosis)
            .Join(_db.Examinations, diagnosis => diagnosis.ExaminationId, examination => examination.Id, (_, examination) => examination)
            .Join(_db.Pets, examination => examination.PetId, pet => pet.Id, (_, pet) => pet)
            .AnyAsync(pet => pet.OwnerId == ownerId, cancellationToken);
    }
}
