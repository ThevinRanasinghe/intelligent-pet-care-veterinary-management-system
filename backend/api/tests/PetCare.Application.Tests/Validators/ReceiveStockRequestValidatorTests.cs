using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Validators;
using Xunit;

namespace PetCare.Application.Tests.Validators;

public class ReceiveStockRequestValidatorTests
{
    // TC-BE-016: Receiving zero or negative stock must be rejected.
    [Theory]
    [Trait("TestCase", "TC-BE-016")]
    [InlineData(0)]
    [InlineData(-5)]
    public void ReceiveStock_NonPositiveQuantity_FailsValidation(int quantity)
    {
        // ARRANGE: Keep every field valid except Quantity.
        var request = new ReceiveStockRequest
        {
            SupplierId = Guid.NewGuid(),
            BatchNumber = "TEST-BATCH-001",
            Quantity = quantity,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        };
        var validator = new ReceiveStockRequestValidator();

        // ACT: Execute the actual FluentValidation rules.
        var result = validator.Validate(request);

        // ASSERT: Quantity must be rejected.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(ReceiveStockRequest.Quantity));
    }
}
