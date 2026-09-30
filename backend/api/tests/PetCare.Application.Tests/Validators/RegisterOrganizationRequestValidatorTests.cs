using PetCare.Application.DTOs.Auth;
using PetCare.Application.Validators;
using Xunit;

namespace PetCare.Application.Tests.Validators;

/// <summary>
/// Clinic-location rules on RegisterOrganizationRequest: latitude and
/// longitude are optional but must be supplied together and within range.
/// </summary>
public class RegisterOrganizationRequestValidatorTests
{
    private readonly RegisterOrganizationRequestValidator _validator = new();

    private static RegisterOrganizationRequest ValidRequest() => new()
    {
        OrganizationName = "Happy Paws Veterinary",
        OrganizationEmail = "contact@happypaws.lk",
        OrganizationPhone = "+94 11 234 5678",
        Address = "1 Galle Road",
        City = "Colombo",
        Country = "Sri Lanka",
        ManagerFirstName = "Amal",
        ManagerLastName = "Perera",
        ManagerEmail = "amal@happypaws.lk",
        Password = "SecurePass1!",
        ConfirmPassword = "SecurePass1!",
    };

    [Fact]
    public void Validate_WithoutCoordinates_Passes()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithValidCoordinatePair_Passes()
    {
        var request = ValidRequest();
        request.Latitude = 6.9271;
        request.Longitude = 79.8612;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithLatitudeOutOfRange_Fails()
    {
        var request = ValidRequest();
        request.Latitude = 95;
        request.Longitude = 79.8612;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterOrganizationRequest.Latitude));
    }

    [Fact]
    public void Validate_WithLongitudeOutOfRange_Fails()
    {
        var request = ValidRequest();
        request.Latitude = 6.9271;
        request.Longitude = -181;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterOrganizationRequest.Longitude));
    }

    [Fact]
    public void Validate_WithLatitudeButNoLongitude_Fails()
    {
        var request = ValidRequest();
        request.Latitude = 6.9271;
        request.Longitude = null;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithLongitudeButNoLatitude_Fails()
    {
        var request = ValidRequest();
        request.Latitude = null;
        request.Longitude = 79.8612;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }
}
