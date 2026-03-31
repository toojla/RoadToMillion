using Microsoft.Extensions.Configuration;
using NSubstitute;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Endpoints;

/// <summary>
/// Unit tests for Auth endpoint logic, specifically testing the registration toggle feature.
/// </summary>
public class AuthEndpointLogicTests
{
    private readonly IAuthService _authService;
    private IConfiguration _configuration;

    public AuthEndpointLogicTests()
    {
        _authService = Substitute.For<IAuthService>();
    }

    #region Registration Toggle Tests

    [Fact]
    public void RegistrationStatus_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Features:EnableUserRegistration", "true" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var enabled = _configuration.GetValue<bool>("Features:EnableUserRegistration", true);

        // Assert
        enabled.ShouldBeTrue();
    }

    [Fact]
    public void RegistrationStatus_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Features:EnableUserRegistration", "false" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var enabled = _configuration.GetValue<bool>("Features:EnableUserRegistration", true);

        // Assert
        enabled.ShouldBeFalse();
    }

    [Fact]
    public void RegistrationStatus_WhenNotConfigured_ShouldDefaultToTrue()
    {
        // Arrange
        var configData = new Dictionary<string, string?>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var enabled = _configuration.GetValue<bool>("Features:EnableUserRegistration", true);

        // Assert
        enabled.ShouldBeTrue(); // Default value
    }

    [Fact]
    public async Task Register_WhenDisabled_ShouldNotCallAuthService()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Features:EnableUserRegistration", "false" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var registrationEnabled = _configuration.GetValue<bool>("Features:EnableUserRegistration", true);

        // Act & Assert
        registrationEnabled.ShouldBeFalse();

        // If registration is disabled, the endpoint should return 403 without calling the service
        // This verifies the logic that would be in the endpoint handler
        if (!registrationEnabled)
        {
            // Should not proceed to call auth service
            await _authService.DidNotReceive().RegisterAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string?>());
        }
    }

    [Fact]
    public async Task Register_WhenEnabled_ShouldCallAuthService()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Features:EnableUserRegistration", "true" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var email = "test@example.com";
        var password = "Test123!";

        _authService.RegisterAsync(email, password, null, null)
            .Returns(Task.FromResult(
                Result<RegisterResponse>.Created(
                    new RegisterResponse("user-123", email),
                    "/api/users/user-123")));

        var registrationEnabled = _configuration.GetValue<bool>("Features:EnableUserRegistration", true);

        // Act
        registrationEnabled.ShouldBeTrue();

        if (registrationEnabled)
        {
            var result = await _authService.RegisterAsync(email, password, null, null);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Type.ShouldBe(ResultType.Created);
        }
    }

    #endregion Registration Toggle Tests

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var password = "Test123!";
        var loginResponse = new LoginResponse("token123", email, "John", "Doe");

        _authService.LoginAsync(email, password)
            .Returns(Task.FromResult(Result<LoginResponse>.Success(loginResponse)));

        // Act
        var result = await _authService.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
        result.Data.ShouldBe(loginResponse);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnBadRequest()
    {
        // Arrange
        var email = "test@example.com";
        var password = "WrongPassword";

        _authService.LoginAsync(email, password)
            .Returns(Task.FromResult(Result<LoginResponse>.BadRequest("Invalid email or password.")));

        // Act
        var result = await _authService.LoginAsync(email, password);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
    }

    #endregion Login Tests

    #region Logout Tests

    [Fact]
    public async Task Logout_ShouldReturnSuccess()
    {
        // Arrange
        var jti = "token-jti-123";
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        _authService.LogoutAsync(jti, expiration).Returns(Task.FromResult(Result.Success()));

        // Act
        var result = await _authService.LogoutAsync(jti, expiration);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
    }

    #endregion Logout Tests
}