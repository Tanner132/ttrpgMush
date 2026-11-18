namespace SeattleByNight.Domain;

// WF-86: what an interactable does when a runner acts on it.
public static class InteractableKinds
{
    // Its description, and the event content can react to.
    public const string Inspect = "inspect";
    // A flyer, a sign: its description and its text.
    public const string Read = "read";
    // A container: searching it turns up the items inside, for the searcher.
    public const string Search = "search";
    // A switch: using it raises the "used" event, whose effects are content.
    public const string Use = "use";

    public static readonly IReadOnlyList<string> All = [Inspect, Read, Search, Use];

    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind);
}
