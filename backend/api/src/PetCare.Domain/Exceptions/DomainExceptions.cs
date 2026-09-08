namespace PetCare.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class OwnershipValidationException : DomainException
{
    public OwnershipValidationException(string petId, string ownerId)
        : base($"Pet ownership validation failed. Pet '{petId}' does not belong to owner '{ownerId}'.")
    {
    }

    public OwnershipValidationException(string message) : base(message)
    {
    }
}

public class ClinicalRecordModificationForbiddenException : DomainException
{
    public ClinicalRecordModificationForbiddenException(string message = "Owners are forbidden from modifying clinical records. Only authorized veterinary staff may update clinical history.")
        : base(message)
    {
    }
}
