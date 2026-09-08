namespace PetCare.Domain.Enums;

/// <summary>
/// Application role definitions. String values must match <see cref="Constants.Roles"/> constants exactly.
/// </summary>
public enum UserRole
{
    PetOwner,
    Veterinarian,
    InventoryOfficer,
    ClinicManager,
    SuperAdmin
}
