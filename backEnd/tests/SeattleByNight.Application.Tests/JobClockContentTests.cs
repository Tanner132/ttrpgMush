using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// Workstream 8 phase 3: a job's deadline and aftermath stages, as content.
public sealed class JobClockContentTests
{
    private static GameContentDocument LoadWith(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!.AsObject();
        var job = root["missions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => node["id"]!.GetValue<string>() == ShippedContentIds.Missions.GangWarehouseRetrieval.ToString());
        edit(job);
        return GameContentLoader.Load(root.ToJsonString());
    }

    private static MissionDefinition Warehouse(GameContentDocument document) =>
        document.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;

    [Fact]
    public void AJobWithoutADeadline_GetsTheDefault()
    {
        var job = Warehouse(LoadWith(job => job.Remove("deadlineHours")));
        Assert.Equal(JobClockRules.DefaultDeadlineHours, job.DeadlineHours);
        Assert.Empty(job.Aftermath);
    }

    [Fact]
    public void AftermathStages_AreChosenByHoursSinceCompletion()
    {
        var job = Warehouse(LoadWith(job =>
        {
            job["deadlineHours"] = 4;
            job["aftermath"] = JsonNode.Parse("""
                [
                  { "fromHour": 0, "text": "Bodies and a wrecked room." },
                  { "fromHour": 2, "text": "Security teams investigate." },
                  { "fromHour": 12, "text": "Cleaned up." }
                ]
                """);
        }));

        Assert.Equal(4, job.DeadlineHours);
        Assert.Equal("Bodies and a wrecked room.", job.AftermathAt(TimeSpan.FromMinutes(30))?.Text);
        Assert.Equal("Security teams investigate.", job.AftermathAt(TimeSpan.FromHours(2))?.Text);
        Assert.Equal("Security teams investigate.", job.AftermathAt(TimeSpan.FromHours(11.9))?.Text);
        Assert.Equal("Cleaned up.", job.AftermathAt(TimeSpan.FromHours(20))?.Text);
    }

    [Fact]
    public void BeforeTheFirstStage_NothingIsShown()
    {
        var job = Warehouse(LoadWith(job => job["aftermath"] = JsonNode.Parse("""[{ "fromHour": 3, "text": "Later." }]""")));
        Assert.Null(job.AftermathAt(TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("""{ "deadlineHours": 0 }""", "deadline must be 1–72 hours")]
    [InlineData("""{ "deadlineHours": 73 }""", "deadline must be 1–72 hours")]
    [InlineData("""{ "aftermath": [{ "fromHour": 24, "text": "x" }] }""", "must start from hour 0–23")]
    [InlineData("""{ "aftermath": [{ "fromHour": 0, "text": " " }] }""", "needs text")]
    [InlineData("""{ "aftermath": [{ "fromHour": 5, "text": "a" }, { "fromHour": 5, "text": "b" }] }""", "later hours in order")]
    public void BadClocksAreRefused(string fields, string expected)
    {
        var exception = Assert.Throws<GameContentException>(() => LoadWith(job =>
        {
            foreach (var (key, value) in JsonNode.Parse(fields)!.AsObject())
            {
                job[key] = value?.DeepClone();
            }
        }));
        Assert.Contains(expected, exception.Message);
    }
}
