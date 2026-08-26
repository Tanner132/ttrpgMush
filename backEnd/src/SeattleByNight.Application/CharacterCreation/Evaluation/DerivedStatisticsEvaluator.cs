using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record DerivedStatisticsEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalDerivedStatistics? Statistics);

// Final-calculations block (sr5-core p. 101, PDF 103): Essence, Inherent
// Limits, Initiative, Condition Monitor boxes, and Karma/nuyen carryover.
// Deliberately deterministic (no dice rolled) so, unlike starting cash, it is
// always available on every preview, not just at finalize.
public sealed class DerivedStatisticsEvaluator
{
    private const decimal StartingEssence = 6m;
    private const int MaxCarryoverKarma = 7;
    private const int MaxCarryoverNuyen = 5000;

    public DerivedStatisticsEvaluation Evaluate(
        MetatypeAndAttributeEvaluation metatypeEvaluation,
        ResourcesEssenceEvaluation resourcesEvaluation,
        GearAttachmentEvaluation gearAttachmentEvaluation,
        IdentityEvaluation identityEvaluation,
        LifestyleEvaluation lifestyleEvaluation,
        KarmaBudgetEvaluation karmaBudgetEvaluation)
    {
        if (metatypeEvaluation.Attributes.Count == 0)
        {
            return new DerivedStatisticsEvaluation([], null);
        }

        int Attribute(string id) =>
            metatypeEvaluation.Attributes.FirstOrDefault(item => item.Id == id)?.AbsoluteValue ?? 0;

        var body = Attribute("body");
        var reaction = Attribute("reaction");
        var strength = Attribute("strength");
        var willpower = Attribute("willpower");
        var logic = Attribute("logic");
        var intuition = Attribute("intuition");
        var charisma = Attribute("charisma");

        var essence = StartingEssence - (resourcesEvaluation.Resources?.TotalEssenceLoss ?? 0m);

        var physicalLimit = DerivedStatisticsFormulas.PhysicalLimit(strength, body, reaction);
        var mentalLimit = DerivedStatisticsFormulas.MentalLimit(logic, intuition, willpower);
        var socialLimit = DerivedStatisticsFormulas.SocialLimit(charisma, willpower, essence);

        var physicalConditionMonitor = DerivedStatisticsFormulas.PhysicalConditionMonitor(body);
        var stunConditionMonitor = DerivedStatisticsFormulas.StunConditionMonitor(willpower);

        var karmaRemaining = Math.Max(0, karmaBudgetEvaluation.Pool - karmaBudgetEvaluation.Spent);
        var nuyenRemaining = resourcesEvaluation.Resources is null
            ? 0
            : Math.Max(0, resourcesEvaluation.Resources.NuyenBudget + resourcesEvaluation.Resources.NuyenFromKarma
                - resourcesEvaluation.Resources.TotalNuyenSpent
                - (gearAttachmentEvaluation.Attachments?.TotalNuyenSpent ?? 0)
                - (identityEvaluation.Identities?.TotalNuyenSpent ?? 0)
                - (lifestyleEvaluation.Lifestyles?.TotalNuyenSpent ?? 0));

        var statistics = new CanonicalDerivedStatistics(
            essence,
            physicalLimit,
            mentalLimit,
            socialLimit,
            DerivedStatisticsFormulas.InitiativeBase(reaction, intuition),
            DerivedStatisticsFormulas.InitiativeDiceBase,
            physicalConditionMonitor,
            stunConditionMonitor,
            body,
            Math.Min(MaxCarryoverKarma, karmaRemaining),
            Math.Min(MaxCarryoverNuyen, nuyenRemaining));

        return new DerivedStatisticsEvaluation([], statistics);
    }
}
