using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// WF-33: a refused document names the record and field, not the .NET member.
public sealed class GameContentErrorLocatorTests
{
    [Fact]
    public void Locate_NamesTheConversationPartReplyAndField()
    {
        const string json = """
            {"scenes":[{"displayName":"Hallway Ambush","nodes":[{"text":"a"},{"text":"b","choices":[{"label":"x"},{"label":"Ask for more"}]}]}]}
            """;

        var where = GameContentErrorLocator.Locate(json, "$.scenes[0].nodes[1].choices[1].roll");

        Assert.Equal("Conversation “Hallway Ambush” › part 2 › reply 2 “Ask for more” › roll", where);
    }

    [Fact]
    public void Load_UnknownField_SaysWhichReplyHasIt()
    {
        const string json = """
            {"contentId":"c","version":"1","scenes":[{"id":"6d1f2f0e-55c5-4a3b-9e57-5f2e6f4d7c11","displayName":"Hallway Ambush","nodes":[{"id":"7d1f2f0e-55c5-4a3b-9e57-5f2e6f4d7c11","text":"b","choices":[{"id":"8d1f2f0e-55c5-4a3b-9e57-5f2e6f4d7c11","label":"Ask for more","conditions":[],"bogus":1}]}]}]}
            """;

        var exception = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));

        Assert.DoesNotContain(".NET", exception.Message);
        Assert.Contains("Conversation “Hallway Ambush” › part 1 › reply 1 “Ask for more” has a field the server does not know: “bogus”.", exception.Message);
    }
}
