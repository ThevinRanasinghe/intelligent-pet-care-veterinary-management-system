using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Application contract for authentication operations.
/// Implemented in PetCare.Infrastructure to keep database/Identity references out of Application.
/// </summary>
public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);

    Task<CurrentUserDto> RegisterPetOwnerAsync(RegisterPetOwnerRequestDto request, CancellationToken ct = default);

    Task<CurrentUserDto?> GetCurrentUserAsync(string userId, CancellationToken ct = default);
}
