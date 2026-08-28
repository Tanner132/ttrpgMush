using System.Text.Json;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Sheets;

// Normalizes a persisted CharacterSheet row into one typed baseline,
// rejecting anything that isn't the current supported schema version or
// doesn't match its pinned catalog. Only one evaluated schema shape (version
// 3) is supported — there is no historical or legacy character data to
// preserve, so unlike the catalog loader's version-pin registry this reader
// has a single supported version rather than a retained list.
public sealed class CharacterCreationBaselineReader
{
    private readonly IRulesetCatalogProvider catalogProvider;

    public CharacterCreationBaselineReader(IRulesetCatalogProvider catalogProvider)
    {
        this.catalogProvider = catalogProvider;
    }

    public CharacterCreationBaselineResult Read(FinalizedCharacterSheet sheet)
    {
        if (sheet.SheetSchemaVersion != CharacterCreationDocumentVersions.Sheet)
        {
            return CharacterCreationBaselineResult.Failure(CharacterCreationBaselineError.UnsupportedSchemaVersion);
        }

        if (!catalogProvider.TryGet(sheet.RulesetId, sheet.CatalogVersion, out var catalog) || catalog is null)
        {
            return CharacterCreationBaselineResult.Failure(CharacterCreationBaselineError.RulesetCatalogUnavailable);
        }

        // Digest/schema integrity enforcement is intentionally disabled during
        // the pre-alpha active-schema-development phase -- see
        // roadmap/SR5_RULESET_MANIFEST.md "Schema Lifecycle" and the matching
        // comment in RulesetCatalogLoader.Load. Re-enable this block once the
        // base schema is declared stable/locked.
        //
        // if (!string.Equals(catalog.SemanticDigest, sheet.CatalogSemanticDigest, StringComparison.Ordinal))
        // {
        //     return CharacterCreationBaselineResult.Failure(CharacterCreationBaselineError.CatalogDigestMismatch);
        // }

        CanonicalCharacterSheet canonical;
        try
        {
            canonical = CharacterCreationDraftSerialization.DeserializeCanonicalSheet(sheet.CanonicalSheetJson);
        }
        catch (JsonException)
        {
            return CharacterCreationBaselineResult.Failure(CharacterCreationBaselineError.MalformedDocument);
        }

        // Metatype, Resources, DerivedStatistics, and Lifestyles are
        // unconditionally populated by every finalization (see
        // CharacterCreationDraftEvaluator's mandatory-section comment) — their
        // absence means a corrupted or hand-edited row, not a legitimate
        // character state. MagicResonance/GearAttachments/Contacts/
        // Identities/Profile are all legitimately null (e.g. a mundane
        // character has no MagicResonance) and must not be checked here.
        if (canonical.Metatype is null
            || canonical.Resources is null
            || canonical.DerivedStatistics is null
            || canonical.Lifestyles is null)
        {
            return CharacterCreationBaselineResult.Failure(CharacterCreationBaselineError.IncompleteDocument);
        }

        return CharacterCreationBaselineResult.Success(new CharacterCreationBaseline(
            sheet.CharacterId,
            sheet.Name,
            sheet.RulesetId,
            sheet.CatalogVersion,
            sheet.CatalogSemanticDigest,
            sheet.CreationMethodId,
            sheet.SheetSchemaVersion,
            sheet.SourceDraftDigest,
            sheet.FinalizedAtUtc,
            canonical));
    }
}
