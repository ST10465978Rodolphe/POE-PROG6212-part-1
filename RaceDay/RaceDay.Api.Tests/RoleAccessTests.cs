using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class RoleAccessTests : ApiTestBase
{
    public RoleAccessTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Not logged in -> 401 ----------

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/events")]
    [InlineData("/api/events/mine")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    public async Task ProtectedGet_WithoutLogin_Returns401(string url)
    {
        var client = NewClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The filter runs before model validation, so even an empty body gives 401 (not 400)
    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/enrolments")]
    [InlineData("/api/results")]
    public async Task ProtectedPost_WithoutLogin_Returns401(string url)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutLogin_Returns401()
    {
        var client = NewClient();

        var response = await client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest { FirstName = "A", LastName = "B" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Participant using Organiser-only endpoints -> 403 ----------

    [Fact]
    public async Task Participant_CreatesEvent_Returns403()
    {
        var participant = await LoggedInClient("Participant");

        var response = await participant.PostAsJsonAsync("/api/events", NewEventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_UpdatesEvent_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", NewUpdateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_DeletesEvent_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_ViewsMyEvents_Returns403()
    {
        var participant = await LoggedInClient("Participant");

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CreatesCategory_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { CategoryName = "Sneaky", Distance = 5, EntryFee = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_ViewsEventEnrolments_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.GetAsync($"/api/enrolments/event/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CapturesResult_Returns403()
    {
        var participant = await LoggedInClient("Participant");

        var response = await participant.PostAsJsonAsync("/api/results",
            new CreateResultRequest { EnrolmentId = 1, FinishTime = new TimeSpan(0, 50, 0), Position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_ViewsEventResults_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.GetAsync($"/api/results/event/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Organiser using Participant-only endpoints -> 403 ----------

    [Fact]
    public async Task Organiser_EntersEvent_Returns403()
    {
        var (organiser, ev, category) = await NewEventWithCategory();

        var response = await Enrol(organiser, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ViewsMyEnrolments_Returns403()
    {
        var organiser = await LoggedInClient("Organiser");

        var response = await organiser.GetAsync("/api/enrolments/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ViewsMyResults_Returns403()
    {
        var organiser = await LoggedInClient("Organiser");

        var response = await organiser.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Both roles are allowed on shared endpoints ----------

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task BothRoles_CanViewEventsAndProfile(string role)
    {
        var client = await LoggedInClient(role);

        var events = await client.GetAsync("/api/events");
        var profile = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }
}