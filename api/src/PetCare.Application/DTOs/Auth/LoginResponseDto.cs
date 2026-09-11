namespace PetCare.Application.DTOs.Auth;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentUserDto User
);
