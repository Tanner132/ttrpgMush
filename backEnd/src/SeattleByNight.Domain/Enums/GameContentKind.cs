namespace SeattleByNight.Domain.Enums;

// Milestone 7 (§50): the content types the database-backed store holds, one
// row per authored definition. Each kind maps to one array of the merged
// content document the loader parses, so adding a kind is an additive change
// here, in the composer, and in the loader — never per-definition code.
public enum GameContentKind
{
    Encounter,
    Mission,
    // A scene graph. Bound to an NPC template it is that NPC's scene;
    // unbound it is a prompt a trigger opens (Milestone 7).
    Scene,
    // An authored test definition — pool, limit, threshold or opposition.
    Test,
    // An NPC base stat block (Milestone 7 §4): authored once, reused by every
    // placement that names it.
    NpcTemplate,
}
