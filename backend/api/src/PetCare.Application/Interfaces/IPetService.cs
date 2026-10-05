using PetCare.Application.DTOs.Pets;

namespace PetCare.Application.Interfaces;

public interface IPetService
{
    Task<PetDto> CreateAsync(CreatePetDto dto);

    Task<List<PetDto>> GetAllAsync(bool includeArchived = false);

    Task<PetDto?> GetByIdAsync(string id);

    Task<List<PetDto>> GetByOwnerIdAsync(
        string ownerId,
        bool includeArchived = false);

    Task<PetDto?> UpdateAsync(string id, UpdatePetDto dto);

    /// <summary>
    /// Archives a pet: it leaves the owner's active list and cannot enter
    /// new workflows, but all historical records stay intact.
    /// Returns null when the pet does not exist.
    /// </summary>
    Task<PetDto?> ArchiveAsync(string id);

    /// <summary>
    /// Restores an archived pet to the owner's active list.
    /// Returns null when the pet does not exist.
    /// </summary>
    Task<PetDto?> RestoreAsync(string id);

    /// <summary>
    /// Permanently deletes a pet — allowed only when it has no
    /// consultation, appointment, or examination history.
    /// </summary>
    Task<bool> DeleteAsync(string id);
}