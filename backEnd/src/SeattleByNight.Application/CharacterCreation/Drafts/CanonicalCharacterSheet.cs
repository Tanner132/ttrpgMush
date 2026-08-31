using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Domain.Enums;

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
    // Synthesized child row for a bundle component (CHRM-FLESH augmentation
    // bundles): never backed by a document.Resources entry, so it has no
    // draft-level delete affordance — only the bundle header line does.
    Bundle,
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
    CanonicalGearAttachments? GearAttachments = null,
    CanonicalContacts? Contacts = null,
    CanonicalIdentities? Identities = null,
    CanonicalLifestyles? Lifestyles = null,
    CanonicalDerivedStatistics? DerivedStatistics = null,
    CanonicalCharacterProfile? Profile = null,
    CanonicalMartialArts? MartialArts = null);

// Free-form, non-mechanical player profile text (gender, age, physical
// description, concept). Has no Karma/priority cost and no RAW citation, so
// unlike every other Canonical* record it carries no CanonicalProvenance.
// Named CanonicalCharacterProfile rather than CanonicalIdentity because that
// name is already taken by the fake-SIN identity record below.
public sealed record CanonicalCharacterProfile(
    string? Gender,
    string? Age,
    string? EyeColor,
    string? HairColor,
    string? Height,
    string? Weight,
    string? SkinTone,
    string? Handedness,
    string? Concept,
    string? ShortDescription,
    string? Description);

// Server-derived final-calculations block (sr5-core p. 101, PDF 103): Essence,
// Inherent Limits, Initiative, Condition Monitor boxes, and Karma/nuyen
// carryover. Recomputed identically on every preview once attributes and
// Essence are resolvable — unlike CanonicalStartingCash, nothing here is
// randomized, so it is never finalize-only.
public sealed record CanonicalDerivedStatistics(
    decimal Essence,
    int PhysicalLimit,
    int MentalLimit,
    int SocialLimit,
    int InitiativeBase,
    int InitiativeDice,
    int PhysicalConditionMonitor,
    int StunConditionMonitor,
    int ConditionMonitorOverflow,
    int CarryoverKarma,
    int CarryoverNuyen);

public sealed record CanonicalMetatype(string Id, CanonicalProvenance Provenance, string? MetavariantId = null);

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

// BreakReason is always null on the immutable creation baseline; SHEET-907's
// CareerSheetComposer is the only writer, using it to record why a group can
// no longer be raised as a group (SkillGroupBreakReason). Kept on the shared
// Canonical* record rather than a career-only side table so
// SkillAdvancementEvaluator can decide group eligibility purely from the
// composed sheet, matching AttributeAdvancementEvaluator's signature style.
public sealed record CanonicalSkillGroup(
    string Id,
    int Rating,
    CanonicalProvenance Provenance,
    int GrantedRating = 0,
    int TotalRating = 0,
    SkillGroupBreakReason? BreakReason = null);

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
    string? InstanceId = null,
    int? CyberlimbStrengthCustomization = null,
    int? CyberlimbAgilityCustomization = null);

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
    CanonicalProvenance Provenance,
    decimal EssenceLoss = 0m,
    IReadOnlyList<string>? Options = null);

public sealed record CanonicalGearAttachments(
    IReadOnlyList<CanonicalAttachment> Attachments,
    int TotalNuyenSpent,
    decimal TotalEssenceLoss = 0m);

public sealed record CanonicalContact(
    string InstanceId,
    string Name,
    string? Role,
    int Connection,
    int Loyalty,
    int KarmaCost,
    CanonicalProvenance Provenance);

// FreeKarmaPool is natural Charisma x 3 (contact.unused-free-karma);
// GeneralKarmaSpent is the portion of combined contact Karma cost beyond
// that pool, drawn from the shared creation Karma pool and folded into
// KarmaBudgetEvaluator's own spent total. It never converts back the other
// way.
public sealed record CanonicalContacts(
    IReadOnlyList<CanonicalContact> Contacts,
    int FreeKarmaPool,
    int GeneralKarmaSpent);

public sealed record CanonicalIdentity(
    string InstanceId,
    int Rating,
    string Details,
    int NuyenCost,
    CanonicalProvenance Provenance);

// SinInstanceId references a CanonicalIdentity.InstanceId
// (identity.fake-license-link) — a license is meaningless without its
// parent fake SIN.
public sealed record CanonicalLicense(
    string InstanceId,
    string SinInstanceId,
    int Rating,
    string Subject,
    int NuyenCost,
    CanonicalProvenance Provenance);

public sealed record CanonicalIdentities(
    IReadOnlyList<CanonicalIdentity> Identities,
    IReadOnlyList<CanonicalLicense> Licenses,
    int TotalNuyenSpent);

public sealed record CanonicalLifestyle(
    string InstanceId,
    string TierId,
    bool IsPrimary,
    int PrepaidMonths,
    IReadOnlyList<string> OptionIds,
    string? PaymentFormId,
    int? AdditionalPersons,
    int NuyenCost,
    CanonicalProvenance Provenance);

// Persisted only at finalize (starting-cash.randomness): the evaluator that
// produces CanonicalLifestyles re-runs on every preview and must stay
// deterministic, so StartingCash is null on every preview and populated
// exactly once, server-side, during atomic finalization.
public sealed record CanonicalStartingCash(
    int Count,
    int Sides,
    int Multiplier,
    IReadOnlyList<int> Rolls,
    int DiceTotal,
    int Total);

public sealed record CanonicalLifestyles(
    IReadOnlyList<CanonicalLifestyle> Lifestyles,
    int TotalNuyenSpent,
    CanonicalStartingCash? StartingCash = null);

// The style's 7 Karma covers the first technique, so its KarmaCost is 0 and
// the style row carries the 7 (run-gun p. 128, PDF 130). TotalKarmaCost is
// what KarmaBudgetEvaluator folds into the shared creation pool.
public sealed record CanonicalMartialArtTechnique(
    string Id,
    int KarmaCost,
    CanonicalProvenance Provenance);

public sealed record CanonicalMartialArts(
    string StyleId,
    int StyleKarmaCost,
    IReadOnlyList<CanonicalMartialArtTechnique> Techniques,
    int TotalKarmaCost,
    CanonicalProvenance Provenance);
