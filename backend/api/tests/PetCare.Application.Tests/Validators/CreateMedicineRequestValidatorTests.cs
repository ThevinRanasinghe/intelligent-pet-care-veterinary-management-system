using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Validators;
using Xunit;

namespace PetCare.Application.Tests.Validators;

public class CreateMedicineRequestValidatorTests
{
    // TC-BE-015: medicine creation rejects invalid price/reorder values.
    [Theory]
    [InlineData(-1, 0, "UnitPrice")]
    [InlineData(10, -1, "ReorderLevel")]
    public void CreateMedicine_InvalidNumericInput_FailsValidation(
        int price, int reorderLevel, string invalidProperty)
    {
        // ARRANGE: Only one numeric field is invalid in each test case.
        var request = new CreateMedicineRequest
        {
            Name = "Amoxicillin",
            Category = "Antibiotic",
            UnitPrice = price,
            ReorderLevel = reorderLevel
        };

        var validator = new CreateMedicineRequestValidator();

        // ACT: Run the actual FluentValidation rules.
        var result = validator.Validate(request);

        // ASSERT: Request must fail for the specific invalid property.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == invalidProperty);
    }
}
