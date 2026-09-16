using PetCare.Application.DTOs.PetOwners;

namespace PetCare.Application.Interfaces;

public interface IPetOwnerService
{
    Task<PetOwnerDto> CreateAsync(CreatePetOwnerDto dto);

    Task<List<PetOwnerDto>> GetAllAsync();

    Task<PetOwnerDto?> GetByIdAsync(string id);
}