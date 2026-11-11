namespace SeattleByNight.Domain.Enums;

public enum RoomAccessType
{
    Public = 0,
    // §31: a room instantiated for a private encounter instance. Never a
    // valid destination for shared-world movement; only same-instance
    // participants may move within it.
    Instanced = 1,
    // WF-25: a room on the shared map that belongs to a job site and is
    // private to that job's runs. Nobody stands in the map room itself: a
    // runner on the run who walks in is placed in their run's copy (an
    // Instanced room made from it), and everyone else finds the way locked.
    Private = 2
}
