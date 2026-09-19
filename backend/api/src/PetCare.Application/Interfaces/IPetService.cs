using PetCare.Application.DTOs.Pets;

namespace PetCare.Application.Interfaces;

public interface IPetService
{
    Task<PetDto> CreateAsync(CreatePetDto dto);

    Task<List<PetDto>> GetAllAsync();

    Task<PetDto?> GetByIdAsync(string id);

    Task<List<PetDto>> GetByOwnerIdAsync(string ownerId);

    Task<PetDto?> UpdateAsync(string id, UpdatePetDto dto);

    Task<bool> DeleteAsync(string id);
}