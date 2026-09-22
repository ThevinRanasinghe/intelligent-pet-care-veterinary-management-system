namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Change-password request. The caller must already be authenticated;
/// the current password is verified before the new one is applied.
/// </summary>
public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmNewPassword { get; set; } = string.Empty;
}
