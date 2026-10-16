using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using RaceDay.Api.Data;
using RaceDay.Contracts;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RaceDay.Api.Tests;

// Starts the real API in memory but swaps SQL Server for an in-memory database,
// so the tests never touch your real RaceDayApiDb.
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration of the DbContext
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericArguments().Contains(typeof(RaceDayDbContext))))
                .ToList();

            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaceDayDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}

// Shared helpers. Every test class inherits from this.
// Each HttpClient has its own cookie jar, so each client behaves like its own browser
// with its own session (one client = one logged-in user).
public abstract class ApiTestBase : IClassFixture<RaceDayFactory>
{
    protected const string TestPassword = "Test12345";

    protected readonly RaceDayFactory Factory;

    protected ApiTestBase(RaceDayFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient NewClient() => Factory.CreateClient();

    protected static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@test.com";

    protected static RegisterRequest NewRegisterRequest(string email, string role) => new()
    {
        FirstName = "Test",
        LastName = role,
        Email = email,
        Password = TestPassword,
        Role = role
    };

    // Registers a brand-new user with the given role and logs them in
    protected async Task<HttpClient> LoggedInClient(string role)
    {
        var client = NewClient();
        string email = UniqueEmail(role.ToLowerInvariant());

        var register = await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email, role));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return client;
    }

    protected static CreateEventRequest NewEventRequest() => new()
    {
        EventName = "Test Road Run",
        Description = "Test event",
        EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        Location = "Cape Town",
        Distance = 10,
        EventType = "Running"
    };

    protected static UpdateEventRequest NewUpdateRequest() => new()
    {
        EventName = "Renamed Run",
        Description = "Updated event",
        EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(40)),
        Location = "Stellenbosch",
        Distance = 21,
        EventType = "Walking"
    };

    protected static async Task<EventDto> CreateEvent(HttpClient organiser)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", NewEventRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    protected static async Task<CategoryDto> CreateCategory(
        HttpClient organiser, int eventId, string name = "10 KM Open")
    {
        var request = new CreateCategoryRequest
        {
            CategoryName = name,
            Description = "Test category",
            Distance = 10,
            EntryFee = 100
        };

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    protected static Task<HttpResponseMessage> Enrol(HttpClient participant, int eventId, int categoryId) =>
        participant.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = eventId, CategoryId = categoryId });

    protected static async Task<int> GetMyUserId(HttpClient client)
    {
        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/users/me");
        return profile!.UserId;
    }

    // A new Organiser with one event and one category, ready for a Participant to enter
    protected async Task<(HttpClient Organiser, EventDto Event, CategoryDto Category)> NewEventWithCategory()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        return (organiser, ev, category);
    }
}