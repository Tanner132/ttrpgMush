namespace SeattleByNight.Domain.Enums;

// Workstream 8: every room is on the one shared map, and anyone may enter it
// (doors aside — see ExitStates). The column stays for player-owned rooms
// (workstream 7).
public enum RoomAccessType
{
    Public = 0,
}
