using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public enum CanonicalProvenance
{
    Priority,
    SpecialPoints,
    GroupPoints,
    Grant,
    Karma,
    FreePoints,
    Native,
    Nuyen,
}

public sealed record CanonicalCharacterSheet(
    PriorityAssignmentPreview PriorityAssignment,
    CanonicalMetatype? Metatype,
    IReadOnlyList<CanonicalAttribute> Attributes,
    IReadOnlyList<CanonicalAttribute> SpecialAttributes,
    IReadOnlyList<CanonicalQuality> Qualities,
    IReadOnlyList<CanonicalSkill> Skills,
    IReadOnlyList<CanonicalSkillGroup> SkillGroups,
    IReadOnlyList<CanonicalKnowledgeSkill> KnowledgeSkills,
    IReadOnlyList<CanonicalLanguage> Languages,
    IReadOnlyList<CanonicalNativeLanguage> NativeLanguages,
    CanonicalMagicResonance? MagicResonance,
    CanonicalResourcesEssence? Resources,
    CanonicalGearAttachments? GearAttachments = null);

public sealed record CanonicalMetatype(string Id, CanonicalProvenance Provenance);

public sealed record CanonicalAttribute(
    string Id,
    int BaseValue,
    int AllocatedPoints,
    int AbsoluteValue,
    CanonicalProvenance Provenance);

public sealed record CanonicalQuality(
    string Id,
    int Rating,
    int KarmaCost,
    IReadOnlyDictionary<string, string>? Parameters,
    CanonicalProvenance Provenance);

public sealed record CanonicalSkill(
    string Id,
    int Rating,
    int GrantedRating,
    int TotalRating,
    string? Specialization,
    string? Parameter,
    CanonicalProvenance Provenance);

public sealed record CanonicalSkillGroup(
    string Id,
    int Rating,
    CanonicalProvenance Provenance);

public sealed record CanonicalKnowledgeSkill(
    string Name,
    string CategoryId,
    int Rating,
    string? Specialization,
    int PointsSpent,
    CanonicalProvenance Provenance);

public sealed record CanonicalLanguage(
    string Name,
    int Rating,
    string? Specialization,
    int PointsSpent,
    CanonicalProvenance Provenance);

public sealed record CanonicalNativeLanguage(
    string Name,
    CanonicalProvenance Provenance);

public sealed record CanonicalMagicResonance(
    string PathId,
    string? TraditionId,
    string? AspectedValueId,
    IReadOnlyList<string> SkillGrants,
    IReadOnlyList<string> SkillGroupGrants,
    IReadOnlyList<CanonicalFormula> Spells,
    IReadOnlyList<CanonicalFormula> Rituals,
    IReadOnlyList<CanonicalPreparation> Preparations,
    IReadOnlyList<CanonicalAdeptPower> AdeptPowers,
    IReadOnlyList<CanonicalComplexForm> ComplexForms,
    CanonicalMentorSpirit? MentorSpirit,
    int? PurchasedPowerPoints);

public sealed record CanonicalFormula(
    string Id,
    string? Parameter,
    bool Granted,
    CanonicalProvenance Provenance);

public sealed record CanonicalPreparation(
    string SpellId,
    string Trigger,
    int? DelayHours,
    bool Granted,
    CanonicalProvenance Provenance);

public sealed record CanonicalAdeptPower(
    string Id,
    int? Rank,
    string? Parameter,
    decimal PowerPointCost,
    CanonicalProvenance Provenance);

public sealed record CanonicalComplexForm(
    string Id,
    bool Granted,
    CanonicalProvenance Provenance);

public sealed record CanonicalMentorSpirit(
    string Id,
    string? Choice,
    CanonicalProvenance Provenance);

public sealed record CanonicalResource(
    string Id,
    int Quantity,
    int? Rating,
    string? GradeId,
    string? Parameter,
    int NuyenCost,
    decimal EssenceLoss,
    CanonicalProvenance Provenance,
    string? InstanceId = null);

public sealed record CanonicalResourcesEssence(
    IReadOnlyList<CanonicalResource> Resources,
    int NuyenBudget,
    int NuyenFromKarma,
    int TotalNuyenSpent,
    decimal TotalEssenceLoss,
    int? MagicLoss,
    int? ResonanceLoss);

// An attachment is recorded against the host line it was purchased for
// (HostInstanceId, matching a CanonicalResource.InstanceId) with its own
// allocation provenance, independent of the host's own provenance.
public sealed record CanonicalAttachment(
    string HostInstanceId,
    string AccessoryId,
    string? Mount,
    int? Rating,
    int NuyenCost,
    CanonicalProvenance Provenance);

public sealed record CanonicalGearAttachments(
    IReadOnlyList<CanonicalAttachment> Attachments,
    int TotalNuyenSpent);
