namespace SeattleByNight.Domain;

public static class RoomDirections
{
    public const string North = "north";
    public const string Northeast = "northeast";
    public const string East = "east";
    public const string Southeast = "southeast";
    public const string South = "south";
    public const string Southwest = "southwest";
    public const string West = "west";
    public const string Northwest = "northwest";
    public const string Up = "up";
    public const string Down = "down";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        North, Northeast, East, Southeast, South, Southwest, West, Northwest, Up, Down
    };

    public static bool IsValid(string? direction) => direction is not null && All.Contains(direction);

    public static string Opposite(string direction) => direction switch
    {
        North => South,
        Northeast => Southwest,
        East => West,
        Southeast => Northwest,
        South => North,
        Southwest => Northeast,
        West => East,
        Northwest => Southeast,
        Up => Down,
        Down => Up,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown room direction.")
    };
}
