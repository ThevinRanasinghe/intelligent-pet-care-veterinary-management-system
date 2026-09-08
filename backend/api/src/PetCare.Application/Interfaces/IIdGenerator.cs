namespace PetCare.Application.Interfaces;

public interface IIdGenerator
{
    Task<string> GeneratePetIdAsync();
    Task<string> GenerateOwnerIdAsync();
    Task<string> GenerateConsultationIdAsync();
    Task<string> GenerateMedicalRecordIdAsync();
    Task<string> GenerateVaccinationRecordIdAsync();
}
