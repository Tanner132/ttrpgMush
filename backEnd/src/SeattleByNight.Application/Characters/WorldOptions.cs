namespace SeattleByNight.Application.Characters;

public sealed class WorldOptions
{
    public const string SectionName = "World";

    public static readonly Guid DefaultStartingRoomId = new("44444444-4444-4444-4444-444444444444");

    public Guid StartingRoomId { get; set; } = DefaultStartingRoomId;
}
