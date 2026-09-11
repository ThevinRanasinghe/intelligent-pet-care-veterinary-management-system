namespace PetCare.Domain.Constants;

/// <summary>
/// Centralized role name constants. Use these everywhere to guarantee consistency.
/// </summary>
public static class Roles
{
    public const string PetOwner         = "PetOwner";
    public const string Veterinarian     = "Veterinarian";
    public const string InventoryOfficer = "InventoryOfficer";
    public const string ClinicManager    = "ClinicManager";
    public const string SuperAdmin       = "SuperAdmin";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PetOwner, Veterinarian, InventoryOfficer, ClinicManager, SuperAdmin
    };
}
