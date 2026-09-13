// PetCare.Infrastructure/Repositories/MedicineRepository.cs (relevant methods)
public Task<int> TryReserveAsync(Guid medicineId, int quantity, CancellationToken ct = default)
{
    return _context.Medicines
        .Where(m => m.Id == medicineId
                 && m.Status == MedicineStatus.Active
                 && (m.TotalQuantity - m.ReservedQuantity) >= quantity)
        .ExecuteUpdateAsync(setters =>
            setters.SetProperty(m => m.ReservedQuantity, m => m.ReservedQuantity + quantity), ct);
}

public Task<int> ReleaseReservedAsync(Guid medicineId, int quantity, CancellationToken ct = default)
{
    return _context.Medicines
        .Where(m => m.Id == medicineId)
        .ExecuteUpdateAsync(setters =>
            setters.SetProperty(m => m.ReservedQuantity, m => m.ReservedQuantity - quantity), ct);
}