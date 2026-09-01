namespace SeattleByNight.Domain.Enums;

public enum RoomAccessType
{
    Public = 0,
    // §31: a room instantiated for a private encounter instance. Never a
    // valid destination for shared-world movement; only same-instance
    // participants may move within it.
    Instanced = 1
}
