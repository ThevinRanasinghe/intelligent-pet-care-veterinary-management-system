public interface IInventoryService
{
    Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest request, CancellationToken ct = default);
    Task<PagedResult<MedicineResponse>> SearchMedicinesAsync(SearchMedicinesRequest request, CancellationToken ct = default);
    Task<MedicineBatchResponse> ReceiveStockAsync(Guid medicineId, ReceiveStockRequest request, Guid performedByUserId, CancellationToken ct = default);
    Task<ReservationResponse> ReserveMedicineAsync(ReserveMedicineRequest request, Guid requestedByUserId, CancellationToken ct = default);
    Task CancelReservationAsync(Guid reservationId, Guid cancelledByUserId, CancellationToken ct = default);
    Task DispenseReservationAsync(Guid reservationId, Guid performedByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineResponse>> GetLowStockAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MedicineBatchResponse>> GetExpiringSoonAsync(int withinDays, CancellationToken ct = default);
}