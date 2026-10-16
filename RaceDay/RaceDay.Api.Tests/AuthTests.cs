using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class AuthTests : ApiTestBase
{
    public AuthTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Registration ----------

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_ValidRequest_Returns201(string role)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            NewRegisterRequest(UniqueEmail("reg"), role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(role, body!.Role);
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = NewClient();
        var request = NewRegisterRequest(UniqueEmail("dup"), "Participant");

        var first = await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            NewRegisterRequest(UniqueEmail("admin"), "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var client = NewClient();
        var request = NewRegisterRequest(UniqueEmail("short"), "Participant");
        request.Password = "short";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotThePlainPassword()
    {
        var client = NewClient();
        string email = UniqueEmail("hash");

        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, "Participant"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(TestPassword, user.PasswordHash);
        Assert.DoesNotContain(TestPassword, user.PasswordHash);
        Assert.Contains(".", user.PasswordHash); // iterations.salt.hash
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_ValidCredentials_Returns200()
    {
        var client = NewClient();
        string email = UniqueEmail("login");
        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, "Organiser"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = TestPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Organiser", body!.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = NewClient();
        string email = UniqueEmail("wrongpw");
        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, "Participant"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = UniqueEmail("nobody"), Password = TestPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_CreatesSession_ProfileIsAvailable()
    {
        var client = await LoggedInClient("Participant");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Participant", profile!.Role);
    }

    // ---------- Logout ----------

    [Fact]
    public async Task Logout_ClearsSession_ProfileReturns401()
    {
        var client = await LoggedInClient("Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        var profile = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    // ---------- Profile ----------

    [Fact]
    public async Task UpdateProfile_ChangesOnlyOwnProfile()
    {
        var client = await LoggedInClient("Participant");
        var other = await LoggedInClient("Participant");
        int otherId = await GetMyUserId(other);

        var update = await client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest { FirstName = "Updated", LastName = "Name", Phone = "0711111111" });
        var mine = await client.GetFromJsonAsync<ProfileDto>("/api/users/me");
        var othersProfile = await other.GetFromJsonAsync<ProfileDto>("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated", mine!.FirstName);
        Assert.Equal("Test", othersProfile!.FirstName); // the other user is unchanged
        Assert.Equal(otherId, othersProfile.UserId);
    }

    [Fact]
    public async Task UpdateProfile_MissingFirstName_Returns400()
    {
        var client = await LoggedInClient("Organiser");

        var response = await client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest { FirstName = "", LastName = "Name" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}