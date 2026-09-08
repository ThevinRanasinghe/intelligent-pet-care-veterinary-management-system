namespace PetCare.Application.DTOs;

public class CreateMedicalRecordDto
{
    public string? Id { get; set; } // Optional short ID override (e.g., MED-3001)
    public DateTime? RecordDate { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string? VeterinarianName { get; set; }
    public string? ClinicalNotes { get; set; }
}

public class MedicalRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string? VeterinarianName { get; set; }
    public string? ClinicalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateVaccinationRecordDto
{
    public string? Id { get; set; } // Optional short ID override (e.g., VAC-4001)
    public string VaccineName { get; set; } = string.Empty;
    public DateTime DateAdministered { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? VeterinarianName { get; set; }
    public string? BatchNumber { get; set; }
}

public class VaccinationRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;
    public string VaccineName { get; set; } = string.Empty;
    public DateTime DateAdministered { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? VeterinarianName { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PetMedicalHistoryDto
{
    public string PetId { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public List<MedicalRecordDto> MedicalRecords { get; set; } = new();
    public List<VaccinationRecordDto> VaccinationRecords { get; set; } = new();
}
