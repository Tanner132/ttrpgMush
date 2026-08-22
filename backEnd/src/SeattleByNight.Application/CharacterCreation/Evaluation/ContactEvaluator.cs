using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record ContactEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalContacts? Contacts);

// Contacts are Karma-priced, not nuyen-priced, so this evaluator is
// independent of the Resources chain. It depends on MetatypeAndAttributeEvaluation
// only to read resolved natural Charisma (the free-pool size), the same
// sibling-evaluator pattern GearAttachmentEvaluator uses for resourcesEvaluation.
public sealed class ContactEvaluator
{
    private const string Step = "contacts";
    private const int MinConnection = 1;
    private const int MaxConnection = 12;
    private const int MinLoyalty = 1;
    private const int MaxLoyalty = 6;
    private const int MaxCreationTotal = 7;
    private const int FreeKarmaPerCharisma = 3;
    private const int MaxTextLength = 120;

    public ContactEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        MetatypeAndAttributeEvaluation metatypeEvaluation)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var contacts = document.Contacts;
        if (contacts is null)
        {
            return new ContactEvaluation(diagnostics, null);
        }

        var source = FallbackSource(catalog);
        var canonical = new List<CanonicalContact>();
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        var totalKarma = 0;

        foreach (var contact in contacts)
        {
            var path = $"contacts[{contact.InstanceId}]";
            if (!instanceIds.Add(contact.InstanceId))
            {
                diagnostics.Add(Error("contact.instance.duplicate", path, [], source,
                    "Each contact needs a unique instance identifier."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(contact.Name) || contact.Name.Length > MaxTextLength)
            {
                diagnostics.Add(CharacterCreationDiagnosticFactory.TextTooLong(Step, $"{path}.name", source));
            }

            if (contact.Role is { Length: > MaxTextLength })
            {
                diagnostics.Add(CharacterCreationDiagnosticFactory.TextTooLong(Step, $"{path}.role", source));
            }

            if (contact.Connection < MinConnection || contact.Connection > MaxConnection)
            {
                diagnostics.Add(Error("contact.connection.out-of-range", $"{path}.connection", [], source,
                    new Dictionary<string, string>
                    {
                        ["minimum"] = MinConnection.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxConnection.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Choose a Connection rating between 1 and 12."));
            }

            if (contact.Loyalty < MinLoyalty || contact.Loyalty > MaxLoyalty)
            {
                diagnostics.Add(Error("contact.loyalty.out-of-range", $"{path}.loyalty", [], source,
                    new Dictionary<string, string>
                    {
                        ["minimum"] = MinLoyalty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxLoyalty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Choose a Loyalty rating between 1 and 6."));
            }

            var cost = contact.Connection + contact.Loyalty;
            if (cost > MaxCreationTotal)
            {
                diagnostics.Add(Error("contact.creation-cap.exceeded", path, [], source,
                    new Dictionary<string, string>
                    {
                        ["actual"] = cost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxCreationTotal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce Connection plus Loyalty to 7 or less at creation."));
            }

            totalKarma += cost;
            canonical.Add(new CanonicalContact(
                contact.InstanceId, contact.Name, contact.Role, contact.Connection, contact.Loyalty,
                cost, CanonicalProvenance.Karma));
        }

        var naturalCharisma = metatypeEvaluation.Attributes
            .FirstOrDefault(item => item.Id == "charisma")?.AbsoluteValue ?? 0;
        var freeKarmaPool = naturalCharisma * FreeKarmaPerCharisma;
        var generalKarmaSpent = Math.Max(0, totalKarma - freeKarmaPool);

        // Finalization requires the free pool fully spent (contact.unused-free-karma);
        // it never converts to general Karma. Firing this unconditionally whenever the
        // section exists, rather than only at finalize, matches the proven
        // attributes.special-points-underallocated pattern: draft saves are never
        // gated on diagnostics, only IsReadyToFinalize is.
        if (totalKarma < freeKarmaPool)
        {
            diagnostics.Add(Error("contact.free-karma.underallocated", "contacts", [], source,
                new Dictionary<string, string>
                {
                    ["available"] = freeKarmaPool.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["spent"] = totalKarma.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Spend all Karma granted by natural Charisma x 3 on contacts, or add more contacts."));
        }

        return new ContactEvaluation(diagnostics, new CanonicalContacts(canonical, freeKarmaPool, generalKarmaSpent));
    }

    private static SourceCitation FallbackSource(RulesetCatalog catalog)
    {
        var source = catalog.Sources["sr5-core"];
        return new SourceCitation(source.Id, 98, 100);
    }

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, resolution);

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, messageArguments, resolution);
}
