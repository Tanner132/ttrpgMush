namespace SeattleByNight.Application.Authorization;

public static class ApplicationRoles
{
    public const string Administrator = "Administrator";
    public const string WorldBuilder = "WorldBuilder";
    public const string Moderator = "Moderator";

    public static readonly IReadOnlyList<string> All = [Administrator, WorldBuilder, Moderator];
}
