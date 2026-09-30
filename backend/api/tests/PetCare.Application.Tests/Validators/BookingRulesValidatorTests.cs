using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Validators;
using Xunit;

namespace PetCare.Application.Tests.Validators;

/// <summary>
/// Booking-rule validators: owner create/update requests, manager assign
/// requests and vet follow-up requests all share the fixed one-hour slot
/// rules (09:00–17:00 starts, hour-aligned, not in the past).
/// </summary>
public class BookingRulesValidatorTests
{
    private static readonly DateTime Tomorrow = DateTime.Today.AddDays(1);

    private static CreateConsultationRequestDto ValidCreate() => new()
    {
        PetId = "PET-0001",
        OwnerId = "OWN-0001",
        Symptoms = "Limping",
        OrganizationId = Guid.NewGuid(),
        PreferredDate = Tomorrow,
        PreferredTime = new TimeSpan(10, 0, 0)
    };

    // ---------------------------------------------------------------
    // CreateConsultationRequestValidator
    // ---------------------------------------------------------------

    [Fact]
    public void Create_MissingOrganization_Fails()
    {
        var dto = ValidCreate();
        dto.OrganizationId = Guid.Empty;

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.OrganizationId));
    }

    [Fact]
    public void Create_MissingPreferredDate_Fails()
    {
        var dto = ValidCreate();
        dto.PreferredDate = null;

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.PreferredDate));
    }

    [Fact]
    public void Create_MissingPreferredTime_Fails()
    {
        var dto = ValidCreate();
        dto.PreferredTime = null;

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.PreferredTime));
    }

    [Fact]
    public void Create_PastDate_Fails()
    {
        var dto = ValidCreate();
        dto.PreferredDate = DateTime.Today.AddDays(-1);

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_NonHourAlignedTime_Fails()
    {
        var dto = ValidCreate();
        dto.PreferredTime = new TimeSpan(9, 30, 0);

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.PreferredTime));
    }

    [Theory]
    [InlineData(8)]   // before opening
    [InlineData(18)]  // start >= 17:30 boundary — 18:00 slot would end 19:00
    public void Create_SlotOutsideOperatingHours_Fails(int startHour)
    {
        var dto = ValidCreate();
        dto.PreferredTime = new TimeSpan(startHour, 30, 0);

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_StartAtEightAM_Fails()
    {
        var dto = ValidCreate();
        dto.PreferredTime = new TimeSpan(8, 0, 0);

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_ValidTenAMSlot_Passes()
    {
        var result = new CreateConsultationRequestValidator().Validate(ValidCreate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_LastSlotStartAtFivePM_Passes()
    {
        var dto = ValidCreate();
        dto.PreferredTime = new TimeSpan(17, 0, 0);

        var result = new CreateConsultationRequestValidator().Validate(dto);

        Assert.True(result.IsValid);
    }

    // ---------------------------------------------------------------
    // UpdateConsultationRequestValidator
    // ---------------------------------------------------------------

    [Fact]
    public void Update_TimeWithoutDate_Fails()
    {
        var dto = new UpdateConsultationRequestDto
        {
            Symptoms = "Limping",
            PreferredTime = new TimeSpan(10, 0, 0)
        };

        var result = new UpdateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.PreferredDate));
    }

    [Fact]
    public void Update_NonAlignedTime_Fails()
    {
        var dto = new UpdateConsultationRequestDto
        {
            Symptoms = "Limping",
            PreferredDate = Tomorrow,
            PreferredTime = new TimeSpan(10, 30, 0)
        };

        var result = new UpdateConsultationRequestValidator().Validate(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Update_ValidSlotChange_Passes()
    {
        var dto = new UpdateConsultationRequestDto
        {
            Symptoms = "Limping",
            PreferredDate = Tomorrow,
            PreferredTime = new TimeSpan(11, 0, 0)
        };

        var result = new UpdateConsultationRequestValidator().Validate(dto);

        Assert.True(result.IsValid);
    }

    // ---------------------------------------------------------------
    // AssignVeterinarianRequestValidator
    // ---------------------------------------------------------------

    private static AssignVeterinarianRequest ValidAssign() => new()
    {
        VeterinarianId = Guid.NewGuid(),
        Date = DateOnly.FromDateTime(Tomorrow),
        StartTime = new TimeOnly(10, 0)
    };

    [Fact]
    public void Assign_EndTimeNotExactlyOneHour_Fails()
    {
        var request = ValidAssign();
        request.EndTime = new TimeOnly(10, 30);

        var result = new AssignVeterinarianRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.EndTime));
    }

    [Fact]
    public void Assign_StartAtEightAM_Fails()
    {
        var request = ValidAssign();
        request.StartTime = new TimeOnly(8, 0);

        var result = new AssignVeterinarianRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.StartTime));
    }

    [Fact]
    public void Assign_NonHourAlignedStart_Fails()
    {
        var request = ValidAssign();
        request.StartTime = new TimeOnly(10, 30);

        var result = new AssignVeterinarianRequestValidator().Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Assign_OmittedEndTime_Passes()
    {
        var result = new AssignVeterinarianRequestValidator().Validate(ValidAssign());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Assign_CorrectEndTime_Passes()
    {
        var request = ValidAssign();
        request.EndTime = new TimeOnly(11, 0);

        var result = new AssignVeterinarianRequestValidator().Validate(request);

        Assert.True(result.IsValid);
    }

    // ---------------------------------------------------------------
    // CreateFollowUpRequestValidator
    // ---------------------------------------------------------------

    private static CreateFollowUpRequest ValidFollowUp() => new()
    {
        PetId = "PET-0001",
        ExaminationId = Guid.NewGuid(),
        PreferredDate = Tomorrow,
        PreferredTime = new TimeOnly(10, 0),
        Reason = "Recheck after surgery"
    };

    [Fact]
    public void FollowUp_MissingPreferredTime_Fails()
    {
        var request = ValidFollowUp();
        request.PreferredTime = default;

        var result = new CreateFollowUpRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.PreferredTime));
    }

    [Fact]
    public void FollowUp_MissingExaminationId_Fails()
    {
        var request = ValidFollowUp();
        request.ExaminationId = Guid.Empty;

        var result = new CreateFollowUpRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.ExaminationId));
    }

    [Fact]
    public void FollowUp_ValidRequest_Passes()
    {
        var result = new CreateFollowUpRequestValidator().Validate(ValidFollowUp());

        Assert.True(result.IsValid);
    }
}
