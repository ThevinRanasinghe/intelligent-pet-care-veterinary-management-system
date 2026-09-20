using FluentValidation;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Authentication business logic shared by every client through ASP.NET
/// Core. Never returns the password/password hash.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RegisterPetOwnerRequest> _registerPetOwnerValidator;
    private readonly IValidator<RegisterOrganizationRequest> _registerOrganizationValidator;

    public AuthService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RegisterPetOwnerRequest> registerPetOwnerValidator,
        IValidator<RegisterOrganizationRequest> registerOrganizationValidator)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _loginValidator = loginValidator;
        _registerPetOwnerValidator = registerPetOwnerValidator;
        _registerOrganizationValidator = registerOrganizationValidator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null || !user.Active || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        // Enforce organization approval: a ClinicManager (or any staff
        // member) belonging to a Pending/inactive organization cannot log
        // in until a SuperAdmin activates the organization. PetOwner and
        // SuperAdmin accounts are not affiliated with an organization and
        // are unaffected by this check.
        if (user.OrganizationId is not null)
        {
            var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value, cancellationToken);
            if (organization is null || !organization.IsActive || organization.Status != OrganizationStatus.Active)
            {
                throw new OrganizationPendingException(
                    "Your organization is pending verification. " +
                    "Please wait for a Beacon administrator to approve your registration.");
            }
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new LoginResponse
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role
        };
    }

    public async Task<CurrentUserResponse> RegisterPetOwnerAsync(RegisterPetOwnerRequest request, CancellationToken cancellationToken = default)
    {
        await _registerPetOwnerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim();
        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Name = $"{firstName} {lastName}".Trim(),
            FirstName = firstName,
            LastName = lastName,
            Role = Roles.PetOwner,
            Active = true,
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapToCurrentUser(user, null);
    }

    public async Task<CurrentUserResponse> RegisterOrganizationAsync(RegisterOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _registerOrganizationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var managerEmail = request.ManagerEmail.Trim();
        if (await _userRepository.EmailExistsAsync(managerEmail, cancellationToken))
        {
            throw new InvalidOperationException("An account with this manager email already exists.");
        }

        var orgName = request.OrganizationName.Trim();
        var orgEmail = request.OrganizationEmail.Trim();
        if (await _organizationRepository.NameOrEmailExistsAsync(orgName, orgEmail, cancellationToken))
        {
            throw new InvalidOperationException("An organization with this name or email already exists.");
        }

        var organization = new Organization
        {
            Name = orgName,
            RegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber)
                ? null
                : request.RegistrationNumber.Trim(),
            Email = orgEmail,
            Phone = request.OrganizationPhone.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Country = request.Country.Trim(),
            Status = OrganizationStatus.Pending,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _organizationRepository.AddAsync(organization, cancellationToken);
        await _organizationRepository.SaveChangesAsync(cancellationToken);

        var managerFirstName = request.ManagerFirstName.Trim();
        var managerLastName = request.ManagerLastName.Trim();

        var manager = new User
        {
            Email = managerEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Name = $"{managerFirstName} {managerLastName}".Trim(),
            FirstName = managerFirstName,
            LastName = managerLastName,
            Role = Roles.ClinicManager,
            Active = true,
            MustChangePassword = false,
            OrganizationId = organization.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _userRepository.AddAsync(manager, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapToCurrentUser(manager, organization);
    }

    private static CurrentUserResponse MapToCurrentUser(User user, Organization? organization)
    {
        return new CurrentUserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            FullName = user.Name,
            MustChangePassword = user.MustChangePassword,
            Organization = organization is null
                ? null
                : new OrganizationDto
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Status = organization.Status.ToString(),
                },
        };
    }
}
