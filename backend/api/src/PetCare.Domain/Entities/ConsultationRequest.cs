namespace PetCare.Domain.Entities;

public class ConsultationRequest
{
    public Guid Id { get; set; }
    public Guid PetId { get; set; }
}