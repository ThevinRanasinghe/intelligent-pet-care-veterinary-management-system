using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Infrastructure.Data;

namespace PetCare.Infrastructure.Services;

public class SequentialIdGenerator : IIdGenerator
{
    private readonly PetCareDbContext _context;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public SequentialIdGenerator(PetCareDbContext context)
    {
        _context = context;
    }

    public async Task<string> GeneratePetIdAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var ids = await _context.Pets
                .Select(p => p.Id)
                .ToListAsync();

            var maxNum = ExtractMaxNumericSuffix(ids, "PET", 1000);
            return $"PET-{maxNum + 1}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateOwnerIdAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var ids = await _context.PetOwners
                .Select(o => o.Id)
                .ToListAsync();

            var maxNum = ExtractMaxNumericSuffix(ids, "OWN", 2000);
            return $"OWN-{maxNum + 1}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateConsultationIdAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var ids = await _context.ConsultationRequests
                .Select(c => c.Id)
                .ToListAsync();

            var maxNum = ExtractMaxNumericSuffix(ids, "REQ", 5000);
            return $"REQ-{maxNum + 1}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateMedicalRecordIdAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var ids = await _context.MedicalRecords
                .Select(m => m.Id)
                .ToListAsync();

            var maxNum = ExtractMaxNumericSuffix(ids, "MED", 3000);
            return $"MED-{maxNum + 1}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateVaccinationRecordIdAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var ids = await _context.VaccinationRecords
                .Select(v => v.Id)
                .ToListAsync();

            var maxNum = ExtractMaxNumericSuffix(ids, "VAC", 4000);
            return $"VAC-{maxNum + 1}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static int ExtractMaxNumericSuffix(IEnumerable<string> ids, string prefix, int defaultBase)
    {
        var regex = new Regex($@"^{prefix}-(\d+)$", RegexOptions.IgnoreCase);
        var max = defaultBase;

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            var match = regex.Match(id);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
            {
                if (num > max)
                {
                    max = num;
                }
            }
        }

        return max;
    }
}
