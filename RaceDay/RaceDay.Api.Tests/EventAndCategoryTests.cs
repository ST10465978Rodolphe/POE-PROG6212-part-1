using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventTests : ApiTestBase
{
    public EventTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Organiser_CreatesEvent_Returns201_AndLinksToOrganiser()
    {
        var organiser = await LoggedInClient("Organiser");
        int organiserId = await GetMyUserId(organiser);

        var response = await organiser.PostAsJsonAsync("/api/events", NewEventRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal(organiserId, created!.OrganiserId);
        Assert.Equal("Test Road Run", created.EventName);
        Assert.Equal("Running", created.EventType);
    }

    [Fact]
    public async Task Organiser_CreatesEvent_InvalidType_Returns400()
    {
        var organiser = await LoggedInClient("Organiser");
        var request = NewEventRequest();
        request.EventType = "Swimming";

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreatesEvent_MissingName_Returns400()
    {
        var organiser = await LoggedInClient("Organiser");
        var request = NewEventRequest();
        request.EventName = "";

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEvents_Returns200()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);
        var participant = await LoggedInClient("Participant");

        var list = await participant.GetAsync("/api/events");
        var single = await participant.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var participant = await LoggedInClient("Participant");

        var response = await participant.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesOwnEvent_Returns200()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", NewUpdateRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await organiser.GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}");
        Assert.Equal("Renamed Run", updated!.EventName);
        Assert.Equal("Walking", updated.EventType);
    }

    [Fact]
    public async Task Organiser_UpdatesAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoggedInClient("Organiser");
        var intruder = await LoggedInClient("Organiser");
        var ev = await CreateEvent(owner);

        var response = await intruder.PutAsJsonAsync($"/api/events/{ev.EventId}", NewUpdateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesOwnEvent_Returns200_ThenEventIsGone()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        var after = await organiser.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoggedInClient("Organiser");
        var intruder = await LoggedInClient("Organiser");
        var ev = await CreateEvent(owner);

        var response = await intruder.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesEventWithEnrolments_Returns409()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        await Enrol(participant, ev.EventId, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyTheLoggedInOrganisersEvents()
    {
        var a = await LoggedInClient("Organiser");
        var b = await LoggedInClient("Organiser");
        int aId = await GetMyUserId(a);
        await CreateEvent(a);
        await CreateEvent(b);

        var mine = await a.GetFromJsonAsync<List<EventDto>>("/api/events/mine");

        Assert.NotEmpty(mine!);
        Assert.All(mine!, e => Assert.Equal(aId, e.OrganiserId));
    }
}

public class CategoryTests : ApiTestBase
{
    public CategoryTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Organiser_CreatesCategory_Returns201()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);

        var category = await CreateCategory(organiser, ev.EventId, "21 KM Open");

        Assert.Equal(ev.EventId, category.EventId);
        Assert.Equal("21 KM Open", category.CategoryName);
    }

    [Fact]
    public async Task Organiser_CreatesDuplicateCategoryName_Returns409()
    {
        var (organiser, ev, _) = await NewEventWithCategory();

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { CategoryName = "10 KM Open", Distance = 10, EntryFee = 50 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreatesCategory_InvalidDistance_Returns400()
    {
        var organiser = await LoggedInClient("Organiser");
        var ev = await CreateEvent(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { CategoryName = "Bad", Distance = 0, EntryFee = 50 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_AddsCategoryToAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoggedInClient("Organiser");
        var intruder = await LoggedInClient("Organiser");
        var ev = await CreateEvent(owner);

        var response = await intruder.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { CategoryName = "Sneaky", Distance = 5, EntryFee = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewCategories_Returns200()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.Single(categories!);
    }

    [Fact]
    public async Task Organiser_UpdatesAndDeletesOwnCategory()
    {
        var (organiser, ev, category) = await NewEventWithCategory();

        var update = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new CreateCategoryRequest { CategoryName = "10 KM Elite", Distance = 10, EntryFee = 200 });
        var delete = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");
        var list = await organiser.GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Organiser_DeletesCategoryWithEnrolments_Returns409()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        await Enrol(participant, ev.EventId, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}