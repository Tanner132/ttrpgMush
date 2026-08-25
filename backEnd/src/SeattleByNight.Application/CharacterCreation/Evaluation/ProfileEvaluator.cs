using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record ProfileEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalCharacterProfile? Profile);

// Profile fields (gender, age, physical description, concept) are free-form,
// non-mechanical player text with no Karma cost and no RAW rule of their own,
// the same situation ContactEvaluator's Name/Role fields are in. This
// evaluator exists only so document.Identity survives finalization instead of
// being silently dropped (it was never read into CanonicalCharacterSheet
// before SHEET-902).
public sealed class ProfileEvaluator
{
    private const string Step = "identity";
    private const int MaxShortTextLength = 120;
    private const int MaxDescriptionLength = 4000;

    public ProfileEvaluation Evaluate(RulesetCatalog catalog, CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var identity = document.Identity;
        if (identity is null)
        {
            return new ProfileEvaluation(diagnostics, null);
        }

        var source = FallbackSource(catalog);

        CheckLength(identity.Gender, "gender", source, diagnostics);
        CheckLength(identity.Age, "age", source, diagnostics);
        CheckLength(identity.EyeColor, "eyeColor", source, diagnostics);
        CheckLength(identity.HairColor, "hairColor", source, diagnostics);
        CheckLength(identity.Height, "height", source, diagnostics);
        CheckLength(identity.Weight, "weight", source, diagnostics);
        CheckLength(identity.SkinTone, "skinTone", source, diagnostics);
        CheckLength(identity.Handedness, "handedness", source, diagnostics);
        CheckLength(identity.Concept, "concept", source, diagnostics);
        CheckLength(identity.ShortDescription, "shortDescription", source, diagnostics);
        CheckLength(identity.Description, "description", source, diagnostics, MaxDescriptionLength);

        var profile = new CanonicalCharacterProfile(
            identity.Gender,
            identity.Age,
            identity.EyeColor,
            identity.HairColor,
            identity.Height,
            identity.Weight,
            identity.SkinTone,
            identity.Handedness,
            identity.Concept,
            identity.ShortDescription,
            identity.Description);

        return new ProfileEvaluation(diagnostics, profile);
    }

    private static void CheckLength(
        string? value,
        string fieldName,
        SourceCitation source,
        List<CharacterCreationDiagnostic> diagnostics,
        int maxLength = MaxShortTextLength)
    {
        if (value is { Length: > 0 } && value.Length > maxLength)
        {
            diagnostics.Add(CharacterCreationDiagnosticFactory.TextTooLong(Step, $"identity.{fieldName}", source, maxLength));
        }
    }

    private static SourceCitation FallbackSource(RulesetCatalog catalog)
    {
        var source = catalog.Sources["sr5-core"];
        return new SourceCitation(source.Id, 64, 66);
    }
}
