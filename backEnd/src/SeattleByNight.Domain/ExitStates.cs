namespace SeattleByNight.Domain;

// Workstream 8: the state of a door, as one array on the exit. An empty array
// is an open door; there is no stored "unlocked". Door state is shared world
// state: there is no per-run copy of it.
//
// - hidden: not shown, and cannot be used, until found.
// - locked: shown, and cannot be used.
// - restricted: nobody gets in, but anyone inside can get out. Set on the
//   exits leading INTO a room (exits are one-way rows). An admin can set it by
//   hand; combat restricts a room while a fight is on (computed, not stored).
// - private: the room beyond belongs to a player (workstream 7). Refuses
//   entry until ownership exists.
public static class ExitStates
{
    public const string Locked = "locked";
    public const string Hidden = "hidden";
    public const string Private = "private";
    public const string Restricted = "restricted";

    public static IReadOnlyList<string> All { get; } = [Locked, Hidden, Private, Restricted];

    public static bool IsValid(string? state) => state is not null && All.Contains(state, StringComparer.Ordinal);

    // The stored form: known values only, each once, in a fixed order, so two
    // exits in the same state compare equal and the audit log reads the same.
    public static string[] Normalize(IEnumerable<string>? states) =>
        states is null ? [] : All.Where(state => states.Contains(state, StringComparer.Ordinal)).ToArray();

    // The two states the map had before workstream 8, for building an exit
    // from flags (tests, seed data).
    public static string[] Of(bool hidden = false, bool locked = false) =>
        Normalize([.. hidden ? [Hidden] : Array.Empty<string>(), .. locked ? [Locked] : Array.Empty<string>()]);
}
