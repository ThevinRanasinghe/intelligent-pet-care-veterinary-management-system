using PetCare.Application.DTOs.Auth;
using PetCare.Application.Validators;
using Xunit;

namespace PetCare.Application.Tests.Validators;

/// <summary>
/// Unit tests for LoginRequestValidator: Email is required and must be a
/// valid email address; Password is required.
/// </summary>
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Passes()
    {
        var result = _validator.Validate(new LoginRequest { Email = "manager@petcare.lk", Password = "ChangeMe123!" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyEmail_Fails()
    {
        var result = _validator.Validate(new LoginRequest { Email = "", Password = "ChangeMe123!" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Validate_WithMalformedEmail_Fails()
    {
        var result = _validator.Validate(new LoginRequest { Email = "not-an-email", Password = "ChangeMe123!" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Validate_WithEmptyPassword_Fails()
    {
        var result = _validator.Validate(new LoginRequest { Email = "manager@petcare.lk", Password = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }
}
