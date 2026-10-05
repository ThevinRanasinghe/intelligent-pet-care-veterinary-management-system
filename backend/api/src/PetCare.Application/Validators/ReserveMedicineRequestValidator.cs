// ReserveMedicineRequestValidator.cs
using FluentValidation;
using PetCare.Application.DTOs.Inventory;

namespace PetCare.Application.Validators;

public class ReserveMedicineRequestValidator : AbstractValidator<ReserveMedicineRequest>
{
    public ReserveMedicineRequestValidator()
    {
        RuleFor(x => x.MedicineId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
