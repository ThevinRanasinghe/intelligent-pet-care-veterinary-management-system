namespace PetCare.Application.DTOs.Consultations;

/// <summary>One fixed one-hour slot of a working day.</summary>
public class AvailabilitySlotDto
{
    /// <summary>Slot start, "HH:mm" (e.g. "09:00").</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>Slot end, "HH:mm" — always Start + 1 hour.</summary>
    public string End { get; set; } = string.Empty;

    /// <summary>True when at least one veterinarian is free for this slot.</summary>
    public bool Available { get; set; }

    /// <summary>
    /// Active veterinarians free for this slot — lets a manager pick a vet
    /// for a chosen slot. Empty when the slot is unavailable.
    /// </summary>
    public List<Guid> AvailableVeterinarianIds { get; set; } = new();
}

/// <summary>Per-day availability for an organization.</summary>
public class DayAvailabilityDto
{
    /// <summary>The queried day ("yyyy-MM-dd").</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>True when the day has already passed — all slots unavailable.</summary>
    public bool IsPast { get; set; }

    public List<AvailabilitySlotDto> Slots { get; set; } = new();
}

/// <summary>One day entry of the month availability overview.</summary>
public class MonthAvailabilityDayDto
{
    /// <summary>The day ("yyyy-MM-dd").</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>True when at least one slot still has a free veterinarian.</summary>
    public bool Available { get; set; }

    /// <summary>True when the day is not past but no slot has a free vet.</summary>
    public bool FullyBooked { get; set; }

    /// <summary>True when the day has already passed.</summary>
    public bool IsPast { get; set; }
}
