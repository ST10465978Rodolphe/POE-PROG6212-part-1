using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : ApiTestBase
{
    public EnrolmentTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Participant_Enrols_Returns201_AndEnrolmentIsRecorded()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        int participantId = await GetMyUserId(participant);

        var response = await Enrol(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.Equal(participantId, enrolment!.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal("Confirmed", enrolment.Status);

        // The Participant sees it in their own list
        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/mine");
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);

        // The Organiser of the event sees it too
        var forEvent = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/enrolments/event/{ev.EventId}");
        Assert.Single(forEvent!);
        Assert.Equal(participantId, forEvent![0].ParticipantId);
    }

    [Fact]
    public async Task Participant_EnrolsTwiceInSameEvent_Returns409()
    {
        var (_, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var first = await Enrol(participant, ev.EventId, category.CategoryId);
        var second = await Enrol(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolsWithCategoryFromAnotherEvent_Returns400()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var (_, _, otherCategory) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");

        var response = await Enrol(participant, ev.EventId, otherCategory.CategoryId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolsInUnknownEvent_Returns404()
    {
        var participant = await LoggedInClient("Participant");

        var response = await Enrol(participant, 999999, 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolsInPastEvent_Returns400()
    {
        var organiser = await LoggedInClient("Organiser");
        var request = NewEventRequest();
        request.EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        var created = await organiser.PostAsJsonAsync("/api/events", request);
        var ev = (await created.Content.ReadFromJsonAsync<EventDto>())!;
        var category = await CreateCategory(organiser, ev.EventId);
        var participant = await LoggedInClient("Participant");

        var response = await Enrol(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ViewsEnrolmentsOfAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, _) = await NewEventWithCategory();
        var intruder = await LoggedInClient("Organiser");

        var response = await intruder.GetAsync($"/api/enrolments/event/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CancelsEnrolment_ParticipantCanEnrolAgain()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        var first = await Enrol(participant, ev.EventId, category.CategoryId);
        var enrolment = (await first.Content.ReadFromJsonAsync<EnrolmentDto>())!;

        var cancel = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Cancelled" });
        var again = await Enrol(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var reactivated = await again.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.Equal("Confirmed", reactivated!.Status);
    }

    [Fact]
    public async Task Organiser_SetsInvalidStatus_Returns400()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        var response = await Enrol(participant, ev.EventId, category.CategoryId);
        var enrolment = (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;

        var update = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Pending" });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Participant_ChangesEnrolmentStatus_Returns403()
    {
        var (_, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        var response = await Enrol(participant, ev.EventId, category.CategoryId);
        var enrolment = (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;

        var update = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }
}

public class ResultTests : ApiTestBase
{
    public ResultTests(RaceDayFactory factory) : base(factory)
    {
    }

    private static Task<HttpResponseMessage> PostResult(
        HttpClient organiser, int enrolmentId, TimeSpan? time = null) =>
        organiser.PostAsJsonAsync("/api/results", new CreateResultRequest
        {
            EnrolmentId = enrolmentId,
            FinishTime = time ?? new TimeSpan(0, 52, 35),
            Position = 12
        });

    // Organiser + event + a Participant who is enrolled in it
    private async Task<(HttpClient Organiser, HttpClient Participant, EventDto Event, EnrolmentDto Enrolment)>
        EnrolledParticipant()
    {
        var (organiser, ev, category) = await NewEventWithCategory();
        var participant = await LoggedInClient("Participant");
        var response = await Enrol(participant, ev.EventId, category.CategoryId);
        var enrolment = (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
        return (organiser, participant, ev, enrolment);
    }

    [Fact]
    public async Task Organiser_CapturesResult_Returns201_AndParticipantCanSeeIt()
    {
        var (organiser, participant, ev, enrolment) = await EnrolledParticipant();

        var response = await PostResult(organiser, enrolment.EnrolmentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");
        Assert.Single(mine!);
        Assert.Equal(12, mine![0].Position);
        Assert.Equal(new TimeSpan(0, 52, 35), mine[0].FinishTime);
        Assert.Equal(ev.EventName, mine[0].EventName);

        var forEvent = await organiser.GetFromJsonAsync<List<ResultDto>>($"/api/results/event/{ev.EventId}");
        Assert.Single(forEvent!);
    }

    [Fact]
    public async Task Organiser_CapturesSecondResultForSameEnrolment_Returns409()
    {
        var (organiser, _, _, enrolment) = await EnrolledParticipant();

        var first = await PostResult(organiser, enrolment.EnrolmentId);
        var second = await PostResult(organiser, enrolment.EnrolmentId);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AnotherOrganiser_CapturesResult_Returns403()
    {
        var (_, _, _, enrolment) = await EnrolledParticipant();
        var intruder = await LoggedInClient("Organiser");

        var response = await PostResult(intruder, enrolment.EnrolmentId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CapturesResult_ZeroFinishTime_Returns400()
    {
        var (organiser, _, _, enrolment) = await EnrolledParticipant();

        var response = await PostResult(organiser, enrolment.EnrolmentId, TimeSpan.Zero);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CapturesResult_UnknownEnrolment_Returns404()
    {
        var organiser = await LoggedInClient("Organiser");

        var response = await PostResult(organiser, 999999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CapturesResult_ForCancelledEnrolment_Returns400()
    {
        var (organiser, _, _, enrolment) = await EnrolledParticipant();
        await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Cancelled" });

        var response = await PostResult(organiser, enrolment.EnrolmentId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnResults()
    {
        var (organiser, _, _, enrolment) = await EnrolledParticipant();
        await PostResult(organiser, enrolment.EnrolmentId);
        var otherParticipant = await LoggedInClient("Participant");

        var theirs = await otherParticipant.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");

        Assert.Empty(theirs!);
    }
}