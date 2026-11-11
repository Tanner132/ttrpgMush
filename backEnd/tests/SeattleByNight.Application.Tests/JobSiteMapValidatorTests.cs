using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// WF-25: the publish-time checks a job site needs the map for. WF-27: every
// record is named by id, so the map and the content agree on GUIDs.
public sealed class JobSiteMapValidatorTests
{
    private static readonly Guid A = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Street = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Marchetti = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    // A two-room site, a and b, entered at a. Rooms carry no name or
    // description: the map owns those.
    private static GameContentDocument Site() => GameContentLoader.Load($$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "encounters": [{
            "id": "{{Guid.NewGuid()}}", "displayName": "Barrens Clinic", "entryRoomId": "{{A}}",
            "roomIds": ["{{A}}", "{{B}}"]
          }],
          "missions": [], "scenes": [], "tests": [], "npcTemplates": []
        }
        """);

    private static JobSiteMap Map(
        RoomAccessType bAccess = RoomAccessType.Private,
        bool connected = true,
        bool includeB = true)
    {
        var rooms = new Dictionary<Guid, JobSiteMapRoom>
        {
            [A] = new(A, "Clinic Door", RoomAccessType.Private),
        };
        if (includeB)
        {
            rooms[B] = new(B, "Waiting Room", bAccess);
        }

        var exits = connected
            ? new List<JobSiteMapExit> { new(A, B, "east", IsHidden: false, IsLocked: true) }
            : [];
        return new JobSiteMap(rooms, exits);
    }

    [Fact]
    public void A_site_of_private_connected_map_rooms_passes_even_through_a_locked_door()
    {
        JobSiteMapValidator.Validate(Site(), Map());
    }

    [Fact]
    public void A_site_using_a_public_room_fails()
    {
        var error = Assert.Throws<GameContentException>(
            () => JobSiteMapValidator.Validate(Site(), Map(bAccess: RoomAccessType.Public)));
        Assert.Contains("must be Private", error.Message);
    }

    [Fact]
    public void A_site_room_with_no_way_in_from_the_entry_fails()
    {
        var error = Assert.Throws<GameContentException>(
            () => JobSiteMapValidator.Validate(Site(), Map(connected: false)));
        Assert.Contains("no way in", error.Message);
        Assert.Contains("'Waiting Room'", error.Message);
    }

    [Fact]
    public void A_site_using_a_room_that_is_not_on_the_map_fails()
    {
        var error = Assert.Throws<GameContentException>(
            () => JobSiteMapValidator.Validate(Site(), Map(includeB: false)));
        Assert.Contains("not on the map", error.Message);
    }

    // WF-26: a job listening outside its site listens in a public room, and
    // whoever it names has to be standing there.
    private static GameContentDocument JobListeningOnTheStreet() => GameContentLoader.Load($$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "encounters": [{
            "id": "{{ShippedContentIds.Encounters.GangWarehouse}}", "displayName": "Barrens Clinic", "entryRoomId": "{{A}}",
            "roomIds": ["{{A}}", "{{B}}"]
          }],
          "missions": [{
            "id": "{{Guid.NewGuid()}}", "displayName": "M1", "description": "d",
            "encounterId": "{{ShippedContentIds.Encounters.GangWarehouse}}",
            "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
            "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
            "objectives": [{ "id": "{{Guid.NewGuid()}}", "displayName": "O1", "kind": "enterEncounter" }],
            "triggers": [{ "id": "{{Guid.NewGuid()}}", "event": "playerEnteredRoom", "roomId": "{{Street}}",
                           "reactions": [{ "kind": "npcSpeaks", "npcId": "{{Marchetti}}", "text": "Psst." }] }]
          }],
          "scenes": [], "tests": [], "npcTemplates": []
        }
        """);

    private static JobSiteMap MapWithStreet(RoomAccessType access, bool marchettiThere)
    {
        var map = Map();
        var rooms = new Dictionary<Guid, JobSiteMapRoom>(map.Rooms)
        {
            [Street] = new(
                Street, "Street", access,
                marchettiThere ? [new JobSiteMapNpc(Marchetti, "Ms. Marchetti")] : []),
        };
        return map with { Rooms = rooms };
    }

    [Fact]
    public void A_job_listening_in_a_public_room_passes_when_its_npc_stands_there()
    {
        JobSiteMapValidator.Validate(JobListeningOnTheStreet(), MapWithStreet(RoomAccessType.Public, marchettiThere: true));
    }

    [Fact]
    public void A_job_listening_for_someone_not_in_the_room_fails()
    {
        var error = Assert.Throws<GameContentException>(
            () => JobSiteMapValidator.Validate(JobListeningOnTheStreet(), MapWithStreet(RoomAccessType.Public, marchettiThere: false)));
        Assert.Contains("names an NPC who is not in 'Street'", error.Message);
    }

    [Fact]
    public void A_job_listening_in_someone_elses_private_room_fails()
    {
        var error = Assert.Throws<GameContentException>(
            () => JobSiteMapValidator.Validate(
                JobListeningOnTheStreet(), MapWithStreet(RoomAccessType.Private, marchettiThere: true)));
        Assert.Contains("not part of this job", error.Message);
    }
}
