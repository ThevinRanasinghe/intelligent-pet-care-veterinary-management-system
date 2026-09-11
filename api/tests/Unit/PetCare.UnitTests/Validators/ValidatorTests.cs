using FluentAssertions;
using FluentValidation.TestHelper;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Validators.Auth;
using Xunit;

namespace PetCare.UnitTests.Validators;

public sealed class RegisterPetOwnerRequestValidatorTests
{
    private readonly RegisterPetOwnerRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        var request = new RegisterPetOwnerRequestDto(
            "John", "Doe", "john@example.com", "Password1!", "Password1!");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void EmptyFirstName_Fails(string firstName)
    {
        var request = new RegisterPetOwnerRequestDto(
            firstName, "Doe", "john@example.com", "Password1!", "Password1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public void InvalidEmail_Fails(string email)
    {
        var request = new RegisterPetOwnerRequestDto(
            "John", "Doe", email, "Password1!", "Password1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("weak")]       // too short, no uppercase, no digit, no special
    [InlineData("alllower1!")] // no uppercase
    [InlineData("ALLUPPER1!")] // no lowercase
    [InlineData("NoDigit!!")]  // no digit
    [InlineData("NoSpecial1")] // no special character
    public void WeakPassword_Fails(string password)
    {
        var request = new RegisterPetOwnerRequestDto(
            "John", "Doe", "john@example.com", password, password);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void PasswordMismatch_Fails()
    {
        var request = new RegisterPetOwnerRequestDto(
            "John", "Doe", "john@example.com", "Password1!", "Different1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }
}

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Valid_Login_Passes()
    {
        var request = new LoginRequestDto("user@example.com", "anypassword");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_Fails()
    {
        var request = new LoginRequestDto("", "password");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        var request = new LoginRequestDto("notanemail", "password");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void EmptyPassword_Fails()
    {
        var request = new LoginRequestDto("user@example.com", "");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public sealed class RegisterOrganizationRequestValidatorTests
{
    private readonly RegisterOrganizationRequestValidator _validator = new();

    [Fact]
    public void Valid_OrganizationRequest_Passes()
    {
        var request = new RegisterOrganizationRequestDto(
            OrganizationName:   "Paws & Claws Veterinary",
            RegistrationNumber: "VET-98765",
            OrganizationEmail:  "contact@pawsclaws.com",
            OrganizationPhone:  "+1555123456",
            Address:            "100 Animal Hospital Way",
            City:               "Boston",
            Country:            "USA",
            ManagerFirstName:   "Jane",
            ManagerLastName:    "Doc",
            ManagerEmail:       "jane@pawsclaws.com",
            Password:           "Password1!",
            ConfirmPassword:    "Password1!"
        );

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Missing_OrganizationName_Fails()
    {
        var request = new RegisterOrganizationRequestDto(
            OrganizationName:   "",
            RegistrationNumber: null,
            OrganizationEmail:  "contact@pawsclaws.com",
            OrganizationPhone:  "+1555123456",
            Address:            "100 Animal Hospital Way",
            City:               "Boston",
            Country:            "USA",
            ManagerFirstName:   "Jane",
            ManagerLastName:    "Doc",
            ManagerEmail:       "jane@pawsclaws.com",
            Password:           "Password1!",
            ConfirmPassword:    "Password1!"
        );

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.OrganizationName);
    }

    [Fact]
    public void PasswordMismatch_Fails()
    {
        var request = new RegisterOrganizationRequestDto(
            OrganizationName:   "Paws Clinic",
            RegistrationNumber: null,
            OrganizationEmail:  "contact@pawsclaws.com",
            OrganizationPhone:  "+1555123456",
            Address:            "100 Animal Way",
            City:               "Boston",
            Country:            "USA",
            ManagerFirstName:   "Jane",
            ManagerLastName:    "Doc",
            ManagerEmail:       "jane@pawsclaws.com",
            Password:           "Password1!",
            ConfirmPassword:    "DifferentPassword2@"
        );

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }
}
