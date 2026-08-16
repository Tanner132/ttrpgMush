namespace SeattleByNight.Api.Hubs;

public static class RoomGroupNames
{
    public static string For(Guid roomId) => $"room:{roomId:N}";
}
