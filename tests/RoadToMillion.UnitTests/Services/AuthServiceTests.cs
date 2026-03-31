using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService demonstrating login, registration, and JWT token generation.
/// </summary>
public class AuthServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthService _sut;

    public AuthServiceTests()
    {
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        var configData = new Dictionary<string, string?>
        {
            { "Jwt:Key", "TestSecretKeyThatIsAtLeast32CharactersLong!" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:ExpirationInMinutes", "60" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new AuthService(_userManager, _signInManager, configuration);
    }

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnSuccessWithToken()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email,
            FirstName = "John",
            LastName = "Doe"
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.Success));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
        result.Data.ShouldNotBeNull();
        result.Data.Email.ShouldBe(email);
        result.Data.FirstName.ShouldBe("John");
        result.Data.LastName.ShouldBe("Doe");
        result.Data.Token.ShouldNotBeNullOrEmpty();
        
        // Verify the token is a valid JWT (basic check)
        result.Data.Token.Split('.').Length.ShouldBe(3); // JWT has 3 parts separated by dots
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ShouldReturnBadRequest()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var password = "Test123!";

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult<ApplicationUser>(null!));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var email = "test@example.com";
        var password = "WrongPassword";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.Failed));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WithLockedOutUser_ShouldReturnBadRequest()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.LockedOut));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldContain("temporarily locked");
    }

    [Fact]
    public async Task LoginAsync_WithUserWithoutName_ShouldReturnTokenWithNullNames()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email,
            FirstName = null,
            LastName = null
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.Success));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.FirstName.ShouldBeNull();
        result.Data.LastName.ShouldBeNull();
    }

    #endregion

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var email = "newuser@example.com";
        var password = "Test123!";
        var firstName = "Jane";
        var lastName = "Smith";

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult<ApplicationUser>(null!));
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.RegisterAsync(email, password, firstName, lastName);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Created);
        result.Data.ShouldNotBeNull();
        result.Data.Email.ShouldBe(email);
        result.Location.ShouldNotBeNullOrEmpty();

        await _userManager.Received(1).CreateAsync(
            Arg.Is<ApplicationUser>(u => 
                u.Email == email && 
                u.UserName == email &&
                u.FirstName == firstName &&
                u.LastName == lastName),
            password);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ShouldReturnCreatedToPreventEnumeration()
    {
        // Arrange
        var email = "existing@example.com";
        var password = "Test123!";
        var existingUser = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(existingUser)!);

        // Act
        var result = await _sut.RegisterAsync(email, password, null, null);

        // Assert — returns Created to prevent user enumeration (attacker cannot tell if email exists)
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Created);
        result.Data.ShouldNotBeNull();
        result.Data.Email.ShouldBe(email);

        // Verify no user was actually created
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var email = "newuser@example.com";
        var password = "weak";

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult<ApplicationUser>(null!));
        
        var identityErrors = new[]
        {
            new IdentityError { Description = "Password is too short." },
            new IdentityError { Description = "Password must contain uppercase letter." }
        };
        
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), password)
            .Returns(Task.FromResult(IdentityResult.Failed(identityErrors)));

        // Act
        var result = await _sut.RegisterAsync(email, password, null, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldContain("Password is too short");
        result.ErrorMessage.ShouldContain("Password must contain uppercase letter");
    }

    [Fact]
    public async Task RegisterAsync_WithoutOptionalNames_ShouldCreateUser()
    {
        // Arrange
        var email = "newuser@example.com";
        var password = "Test123!";

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult<ApplicationUser>(null!));
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.RegisterAsync(email, password, null, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        await _userManager.Received(1).CreateAsync(
            Arg.Is<ApplicationUser>(u => 
                u.Email == email && 
                u.FirstName == null &&
                u.LastName == null),
            password);
    }

    [Fact]
    public async Task RegisterAsync_ShouldSetUsernameToEmail()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult<ApplicationUser>(null!));
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        await _sut.RegisterAsync(email, password, "John", "Doe");

        // Assert
        await _userManager.Received(1).CreateAsync(
            Arg.Is<ApplicationUser>(u => u.UserName == email),
            password);
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var result = await _sut.LogoutAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
    }

    [Fact]
    public async Task LogoutAsync_WithNullUserId_ShouldStillReturnSuccess()
    {
        // Act
        var result = await _sut.LogoutAsync(null!);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region JWT Token Generation Tests

    [Fact]
    public async Task LoginAsync_GeneratedToken_ShouldContainUserClaims()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            FirstName = "John"
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.Success));

        // Act
        var result = await _sut.LoginAsync(email, password);

        // Assert
        result.Data!.Token.ShouldNotBeNullOrEmpty();
        
        // Decode the JWT to verify claims (basic validation)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Data.Token);
        
        token.Claims.ShouldContain(c => c.Type == "sub" && c.Value == userId);
        token.Claims.ShouldContain(c => c.Type == "email" && c.Value == email);
        token.Issuer.ShouldBe("TestIssuer");
        token.Audiences.ShouldContain("TestAudience");
    }

    [Fact]
    public async Task LoginAsync_GeneratedToken_ShouldHaveCorrectExpiration()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            UserName = email
        };

        _userManager.FindByEmailAsync(email).Returns(Task.FromResult(user)!);
        _signInManager.CheckPasswordSignInAsync(user, password, true)
            .Returns(Task.FromResult(SignInResult.Success));

        var beforeLogin = DateTime.UtcNow;

        // Act
        var result = await _sut.LoginAsync(email, password);

        var afterLogin = DateTime.UtcNow;

        // Assert
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Data!.Token);
        
        // Token should expire in approximately 60 minutes
        var expectedExpiry = beforeLogin.AddMinutes(60);
        token.ValidTo.ShouldBeInRange(expectedExpiry.AddSeconds(-5), afterLogin.AddMinutes(60).AddSeconds(5));
    }

    #endregion
}
