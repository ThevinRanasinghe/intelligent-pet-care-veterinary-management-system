using Moq;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;
using PetCare.Application.Validators;
using PetCare.Domain.Entities;
using Xunit;

namespace PetCare.Application.Tests.Validators;

/// <summary>
/// Unit tests for CreateQuotationRequestValidator / UpdateQuotationRequestValidator /
/// QuotationItemRequestValidator: structural rules from
/// docs/database/scheduling-billing-approval-domain-model.md#billing
/// (Quantity &gt; 0, UnitPrice &gt;= 0, Budget &gt;= 0, allowed categories, appointment
/// existence, and the 1:1 Appointment&lt;-&gt;Quotation constraint).
/// </summary>
public class QuotationValidatorTests
{
    private readonly Mock<IAppointmentRepository> _appointmentRepository = new();
    private readonly Mock<IQuotationRepository> _quotationRepository = new();

    private static readonly Guid AppointmentId = Guid.NewGuid();

    private CreateQuotationRequestValidator CreateValidator() =>
        new(_appointmentRepository.Object, _quotationRepository.Object);

    private void SetUpAppointmentExists(bool exists = true)
    {
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists ? new Appointment { Id = AppointmentId } : null);
    }

    private void SetUpQuotationAlreadyExists(bool exists)
    {
        _quotationRepository
            .Setup(r => r.ExistsForAppointmentAsync(AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
    }

    private static QuotationItemRequest ValidItem() => new()
    {
        Category = "Consultation",
        Description = "General consultation",
        Quantity = 1,
        UnitPrice = 20m
    };

    // 1. A fully valid request passes.
    [Fact]
    public async Task CreateQuotationRequestValidator_ValidRequest_Passes()
    {
        SetUpAppointmentExists();
        SetUpQuotationAlreadyExists(false);

        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 100m,
            Items = new List<QuotationItemRequest> { ValidItem() }
        };

        var result = await CreateValidator().ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    // 2. A non-existent appointment fails validation.
    [Fact]
    public async Task CreateQuotationRequestValidator_AppointmentDoesNotExist_Fails()
    {
        SetUpAppointmentExists(exists: false);
        SetUpQuotationAlreadyExists(false);

        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 100m,
            Items = new List<QuotationItemRequest> { ValidItem() }
        };

        var result = await CreateValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Appointment does not exist.");
    }

    // 3. The 1:1 Appointment<->Quotation rule: a second quotation for the same appointment fails.
    [Fact]
    public async Task CreateQuotationRequestValidator_QuotationAlreadyExistsForAppointment_Fails()
    {
        SetUpAppointmentExists();
        SetUpQuotationAlreadyExists(exists: true);

        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 100m,
            Items = new List<QuotationItemRequest> { ValidItem() }
        };

        var result = await CreateValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A quotation already exists for this appointment.");
    }

    // 4. Negative budget fails.
    [Fact]
    public async Task CreateQuotationRequestValidator_NegativeBudget_Fails()
    {
        SetUpAppointmentExists();
        SetUpQuotationAlreadyExists(false);

        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = -1m,
            Items = new List<QuotationItemRequest> { ValidItem() }
        };

        var result = await CreateValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    // 5. Zero budget is allowed (>= 0).
    [Fact]
    public async Task CreateQuotationRequestValidator_ZeroBudget_Passes()
    {
        SetUpAppointmentExists();
        SetUpQuotationAlreadyExists(false);

        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 0m,
            Items = new List<QuotationItemRequest>()
        };

        var result = await CreateValidator().ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    // 6. Quantity <= 0 fails.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuotationItemRequestValidator_NonPositiveQuantity_Fails(int quantity)
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = "Consultation", Description = "x", Quantity = quantity, UnitPrice = 10m };

        var result = validator.Validate(item);

        Assert.False(result.IsValid);
    }

    // 7. Negative unit price fails.
    [Fact]
    public void QuotationItemRequestValidator_NegativeUnitPrice_Fails()
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = "Consultation", Description = "x", Quantity = 1, UnitPrice = -0.01m };

        var result = validator.Validate(item);

        Assert.False(result.IsValid);
    }

    // 8. Zero unit price is allowed (>= 0).
    [Fact]
    public void QuotationItemRequestValidator_ZeroUnitPrice_Passes()
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = "Consultation", Description = "x", Quantity = 1, UnitPrice = 0m };

        var result = validator.Validate(item);

        Assert.True(result.IsValid);
    }

    // 9. Unknown category fails.
    [Fact]
    public void QuotationItemRequestValidator_UnknownCategory_Fails()
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = "Surgery", Description = "x", Quantity = 1, UnitPrice = 10m };

        var result = validator.Validate(item);

        Assert.False(result.IsValid);
    }

    // 10. Every allowed category passes.
    [Theory]
    [InlineData("Consultation")]
    [InlineData("Examination")]
    [InlineData("Treatment")]
    [InlineData("Medicine")]
    [InlineData("Other")]
    public void QuotationItemRequestValidator_AllowedCategories_Pass(string category)
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = category, Description = "x", Quantity = 1, UnitPrice = 10m };

        var result = validator.Validate(item);

        Assert.True(result.IsValid);
    }

    // 11. Empty description fails.
    [Fact]
    public void QuotationItemRequestValidator_EmptyDescription_Fails()
    {
        var validator = new QuotationItemRequestValidator();
        var item = new QuotationItemRequest { Category = "Consultation", Description = "", Quantity = 1, UnitPrice = 10m };

        var result = validator.Validate(item);

        Assert.False(result.IsValid);
    }

    // 12. UpdateQuotationRequestValidator: negative budget fails.
    [Fact]
    public void UpdateQuotationRequestValidator_NegativeBudget_Fails()
    {
        var validator = new UpdateQuotationRequestValidator();
        var request = new UpdateQuotationRequest { Budget = -5m, Items = new List<QuotationItemRequest> { ValidItem() } };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    // 13. UpdateQuotationRequestValidator: valid request passes.
    [Fact]
    public void UpdateQuotationRequestValidator_ValidRequest_Passes()
    {
        var validator = new UpdateQuotationRequestValidator();
        var request = new UpdateQuotationRequest { Budget = 100m, Items = new List<QuotationItemRequest> { ValidItem() } };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
