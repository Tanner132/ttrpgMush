using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCareer;

// Target Architecture > "Composed Sheet": immutable creation baseline +
// permanent career progression = current permanent character sheet. Never
// mutates the baseline instance passed in; the persisted
// character_sheets.canonical_sheet row stays byte-for-byte unchanged.
public sealed class CareerSheetComposer
{
    public CanonicalCharacterSheet Compose(CanonicalCharacterSheet baseline, CareerProgressionDocument progression)
    {
        if (progression.AttributeIncreases.Count == 0)
        {
            return baseline;
        }

        var attributes = ApplyIncreases(baseline.Attributes, progression.AttributeIncreases);
        var specialAttributes = ApplyIncreases(baseline.SpecialAttributes, progression.AttributeIncreases);

        var derivedStatistics = baseline.DerivedStatistics is null
            ? null
            : Recompute(baseline.DerivedStatistics, attributes);

        return baseline with
        {
            Attributes = attributes,
            SpecialAttributes = specialAttributes,
            DerivedStatistics = derivedStatistics,
        };
    }

    private static IReadOnlyList<CanonicalAttribute> ApplyIncreases(
        IReadOnlyList<CanonicalAttribute> source,
        IReadOnlyDictionary<string, int> increases)
    {
        if (source.Count == 0)
        {
            return source;
        }

        return source
            .Select(attribute => increases.TryGetValue(attribute.Id, out var increase) && increase != 0
                ? attribute with { AbsoluteValue = attribute.AbsoluteValue + increase }
                : attribute)
            .ToArray();
    }

    private static CanonicalDerivedStatistics Recompute(
        CanonicalDerivedStatistics baseline,
        IReadOnlyList<CanonicalAttribute> attributes)
    {
        int Value(string id) => attributes.FirstOrDefault(item => item.Id == id)?.AbsoluteValue ?? 0;

        var body = Value("body");
        var reaction = Value("reaction");
        var strength = Value("strength");
        var willpower = Value("willpower");
        var logic = Value("logic");
        var intuition = Value("intuition");
        var charisma = Value("charisma");

        return baseline with
        {
            PhysicalLimit = DerivedStatisticsFormulas.PhysicalLimit(strength, body, reaction),
            MentalLimit = DerivedStatisticsFormulas.MentalLimit(logic, intuition, willpower),
            SocialLimit = DerivedStatisticsFormulas.SocialLimit(charisma, willpower, baseline.Essence),
            InitiativeBase = DerivedStatisticsFormulas.InitiativeBase(reaction, intuition),
            PhysicalConditionMonitor = DerivedStatisticsFormulas.PhysicalConditionMonitor(body),
            StunConditionMonitor = DerivedStatisticsFormulas.StunConditionMonitor(willpower),
            ConditionMonitorOverflow = body,
        };
    }
}
