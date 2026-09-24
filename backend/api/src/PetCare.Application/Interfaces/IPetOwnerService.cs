using PetCare.Application.DTOs.PetOwners;

namespace PetCare.Application.Interfaces;

public interface IPetOwnerService
{
    /// <summary>
    /// Creates a PetOwner profile. When the caller is a PetOwner, pass the
    /// authenticated user's id so the profile is linked (PetOwner.UserId);
    /// staff-created profiles stay unlinked (null).
    /// </summary>
    Task<PetOwnerDto> CreateAsync(CreatePetOwnerDto dto, Guid? userId = null);

    Task<List<PetOwnerDto>> GetAllAsync();

    Task<PetOwnerDto?> GetByIdAsync(string id);
}