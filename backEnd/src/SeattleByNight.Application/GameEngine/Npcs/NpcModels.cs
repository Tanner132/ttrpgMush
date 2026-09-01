using SeattleByNight.Domain.Enums;
using SeattleByNight.Application.GameEngine.Runtime;

namespace SeattleByNight.Application.GameEngine.Npcs;

// A persisted NPC instance (§27) as the engine sees it.
public sealed record NpcSnapshot(
    Guid Id,
    string TemplateId,
    string Name,
    Guid RoomId,
    int PhysicalDamage,
    int StunDamage,
    NpcAwareness Awareness);

public sealed record NewNpcInstance(string TemplateId, string Name, Guid RoomId);

public static class NpcDerivedValues
{
    // NPCs share the player wound-modifier formula (dev decision
    // npc.wound-modifier-shared-formula in roadmap/SR5_RULE_DECISIONS.md).
    public static int WoundModifier(NpcSnapshot npc)
        => RuntimeDerivedValues.WoundModifier(npc.PhysicalDamage, npc.StunDamage);

    // Incapacitation is derived, never stored: either monitor filled means down.
    public static bool IsIncapacitated(NpcSnapshot npc, NpcTemplate template)
        => npc.PhysicalDamage >= template.PhysicalMonitor || npc.StunDamage >= template.StunMonitor;
}
