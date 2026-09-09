namespace PetCare.Application.DTOs;

public class CreatePetDto
{
    public string? Id { get; set; } // Optional short ID override (e.g., PET-1001)
    public string? OwnerId { get; set; } // e.g., OWN-2001 (optional when providing Owner)
    public CreatePetOwnerDto? Owner { get; set; } // New owner details when OwnerId not supplied
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
}

/// <summary>
/// Owner profile update DTO. Notice: strictly excludes clinical/medical records
/// to fulfill UC-06/UC-07 (Prevent owners from modifying clinical records).
/// </summary>
public class UpdatePetProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
}

public class PetResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
