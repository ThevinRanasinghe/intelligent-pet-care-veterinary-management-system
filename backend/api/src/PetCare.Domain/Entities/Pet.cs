namespace PetCare.Domain.Entities;

// Merge ekedi Member 1 ge real Pet entity ekka tama replace karanna one

public class Pet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
}