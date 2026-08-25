using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record MagicResonanceEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    IReadOnlyList<CanonicalAttribute> SpecialAttributes,
    CanonicalMagicResonance? MagicResonance);

public sealed class MagicResonanceEvaluator
{
    private const string Step = "awakening-emergence";
    private const int NaturalAttributeMax = 6;
    private const int ExceptionalAttributeMax = 7;
    private const int MaxTextLength = 120;

    public MagicResonanceEvaluation Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var magicCell = catalog.GetPriorityCell("magic-resonance", assignment.MagicOrResonance);
        if (magicCell is null)
        {
            return new MagicResonanceEvaluation(diagnostics, [], null);
        }

        var selection = document.MagicResonance;
        if (selection is null)
        {
            if (magicCell.MagicResonancePathGrants is { Count: > 0 })
            {
                diagnostics.Add(Error("magic.path.required", "magicResonance.pathId", [], magicCell.Source,
                    "Select a creation path for the assigned Magic or Resonance priority."));
            }

            return new MagicResonanceEvaluation(diagnostics, [], null);
        }

        if (!catalog.CreationPaths.TryGetValue(selection.PathId, out var path))
        {
            diagnostics.Add(Unknown(selection.PathId, catalog, "magicResonance.pathId"));
            return new MagicResonanceEvaluation(diagnostics, [], null);
        }

        var grant = magicCell.MagicResonancePathGrants?.FirstOrDefault(item => item.PathId == selection.PathId);
        if (grant is null)
        {
            diagnostics.Add(Error("magic.path.unavailable", "magicResonance.pathId", [selection.PathId], magicCell.Source,
                new Dictionary<string, string> { ["priorityLevel"] = magicCell.LevelId },
                "Choose a creation path available at the assigned Magic or Resonance priority."));
            return new MagicResonanceEvaluation(diagnostics, [], null);
        }

        var special = document.SpecialAttributes?.Values ?? new Dictionary<string, int>();
        var magicValue = special.GetValueOrDefault("magic");
        var resonanceValue = special.GetValueOrDefault("resonance");

        EvaluateAttributeAllocation(catalog, path, grant, selection, document, magicValue, resonanceValue, diagnostics);
        EvaluateTradition(catalog, path, selection, diagnostics);
        EvaluateAspectedValue(catalog, path, selection, diagnostics);
        EvaluateSkillGrants(catalog, selection, grant, diagnostics);
        EvaluateFormulae(catalog, path, selection, grant, magicValue, diagnostics);
        EvaluatePowerPoints(catalog, path, selection, grant, magicValue, diagnostics);
        EvaluateComplexForms(catalog, path, selection, grant, resonanceValue, document, diagnostics);
        EvaluateMentorSpirit(catalog, path, selection, document, diagnostics);

        var specialAttributes = new List<CanonicalAttribute>();
        if (path.AttributeId is not null)
        {
            var allocated = path.AttributeId == "magic" ? magicValue : resonanceValue;
            specialAttributes.Add(new CanonicalAttribute(
                path.AttributeId,
                grant.AttributeRating,
                allocated,
                grant.AttributeRating + allocated,
                CanonicalProvenance.SpecialPoints));
        }

        return new MagicResonanceEvaluation(diagnostics, specialAttributes, BuildCanonical(catalog, path, selection));
    }

    private static CanonicalMagicResonance BuildCanonical(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection) =>
        new(
            selection.PathId,
            selection.TraditionId,
            selection.AspectedValueId,
            (selection.SkillGrants ?? []).Select(item => item.SkillId).ToArray(),
            (selection.SkillGroupGrants ?? []).Select(item => item.SkillGroupId).ToArray(),
            ToCanonicalFormulae(selection.Spells),
            ToCanonicalFormulae(selection.Rituals),
            ToCanonicalPreparations(selection.Preparations),
            (selection.AdeptPowers ?? []).Select(item => new CanonicalAdeptPower(
                item.PowerId,
                item.Rank,
                item.Parameter,
                PowerCost(catalog, item),
                CanonicalProvenance.Grant)).ToArray(),
            (selection.ComplexForms ?? []).Select(item => new CanonicalComplexForm(
                item.ComplexFormId,
                item.Granted,
                item.Granted ? CanonicalProvenance.Grant : CanonicalProvenance.Karma)).ToArray(),
            selection.MentorSpirit is null
                ? null
                : new CanonicalMentorSpirit(
                    selection.MentorSpirit.MentorSpiritId,
                    selection.MentorSpirit.Choice,
                    CanonicalProvenance.Karma),
            selection.PurchasedPowerPoints);

    private static IReadOnlyList<CanonicalFormula> ToCanonicalFormulae(
        IReadOnlyList<SpellSelection>? spells) =>
        (spells ?? []).Select(item => new CanonicalFormula(
            item.SpellId,
            item.Parameter,
            item.Granted,
            item.Granted ? CanonicalProvenance.Grant : CanonicalProvenance.Karma)).ToArray();

    private static IReadOnlyList<CanonicalFormula> ToCanonicalFormulae(
        IReadOnlyList<RitualSelection>? rituals) =>
        (rituals ?? []).Select(item => new CanonicalFormula(
            item.RitualId,
            null,
            item.Granted,
            item.Granted ? CanonicalProvenance.Grant : CanonicalProvenance.Karma)).ToArray();

    private static IReadOnlyList<CanonicalPreparation> ToCanonicalPreparations(
        IReadOnlyList<PreparationSelection>? preparations) =>
        (preparations ?? []).Select(item => new CanonicalPreparation(
            item.SpellId,
            item.Trigger,
            item.DelayHours,
            item.Granted,
            item.Granted ? CanonicalProvenance.Grant : CanonicalProvenance.Karma)).ToArray();

    private static void EvaluateAttributeAllocation(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonancePathGrant grant,
        MagicResonanceSelection selection,
        CharacterCreationDraftDocument document,
        int magicValue,
        int resonanceValue,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (path.AttributeId is null)
        {
            if (magicValue > 0 || resonanceValue > 0)
            {
                diagnostics.Add(Error("magic.attribute.mundane-forbidden", "specialAttributes", [],
                    path.Source, new Dictionary<string, string>(),
                    "A mundane character cannot hold Magic or Resonance."));
            }

            return;
        }

        var opposing = path.AttributeId == "magic" ? resonanceValue : magicValue;
        if (opposing > 0)
        {
            diagnostics.Add(Error("magic.attribute.mutually-exclusive", "specialAttributes", [],
                path.Source, new Dictionary<string, string> { ["attributeId"] = path.AttributeId },
                "Magic and Resonance are mutually exclusive creation paths."));
        }

        var allocated = path.AttributeId == "magic" ? magicValue : resonanceValue;
        var total = grant.AttributeRating + allocated;
        var naturalMax = CharacterCreationDiagnosticFactory.HasExceptionalAttributeFor(document, path.AttributeId)
            ? ExceptionalAttributeMax
            : NaturalAttributeMax;
        if (total > naturalMax)
        {
            diagnostics.Add(Error("magic.attribute.natural-maximum", "specialAttributes", [path.AttributeId],
                path.Source,
                new Dictionary<string, string>
                {
                    ["attributeId"] = path.AttributeId,
                    ["actual"] = total.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = naturalMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce special attribute points so the total stays within the natural maximum."));
        }
    }

    private static void EvaluateTradition(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (path.RequiresTradition)
        {
            if (string.IsNullOrWhiteSpace(selection.TraditionId))
            {
                diagnostics.Add(Error("magic.tradition.required", "magicResonance.traditionId", [],
                    path.Source, new Dictionary<string, string>(),
                    "Choose the tradition that shapes this character's magical practice."));
            }
            else if (!catalog.Traditions.TryGetValue(selection.TraditionId, out _))
            {
                diagnostics.Add(Unknown(selection.TraditionId, catalog, "magicResonance.traditionId"));
            }
        }
        else if (selection.TraditionId is not null)
        {
            diagnostics.Add(Error("magic.tradition.not-allowed", "magicResonance.traditionId", [],
                path.Source, new Dictionary<string, string>(),
                "This path does not use a magical tradition."));
        }
    }

    private static void EvaluateAspectedValue(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (path.Kind == CreationPathKind.AspectedMagician)
        {
            if (string.IsNullOrWhiteSpace(selection.AspectedValueId))
            {
                diagnostics.Add(Error("magic.aspect.required", "magicResonance.aspectedValueId", [],
                    path.Source, new Dictionary<string, string>(),
                    "Choose exactly one permanent magical aspect."));
            }
            else if (!catalog.AspectedValues.TryGetValue(selection.AspectedValueId, out _))
            {
                diagnostics.Add(Unknown(selection.AspectedValueId, catalog, "magicResonance.aspectedValueId"));
            }
        }
        else if (selection.AspectedValueId is not null)
        {
            diagnostics.Add(Error("magic.aspect.not-allowed", "magicResonance.aspectedValueId", [],
                path.Source, new Dictionary<string, string>(),
                "Only an aspected magician selects a magical aspect."));
        }
    }

    private static void EvaluateSkillGrants(
        RulesetCatalog catalog,
        MagicResonanceSelection selection,
        MagicResonancePathGrant grant,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var expectsSkillGroups = grant.SkillGrants.Any(item => item.Domain == "magical-group");
        var expectsIndividualSkills = grant.SkillGrants.Any(item => item.Domain != "magical-group");
        if (!expectsSkillGroups && (selection.SkillGroupGrants?.Count ?? 0) > 0)
        {
            diagnostics.Add(SkillGrantCount("magical-group", 0, selection.SkillGroupGrants!.Count, "magicResonance.skillGroupGrants"));
        }
        if (!expectsIndividualSkills && (selection.SkillGrants?.Count ?? 0) > 0)
        {
            diagnostics.Add(SkillGrantCount("individual", 0, selection.SkillGrants!.Count, "magicResonance.skillGrants"));
        }

        foreach (var skillGrant in grant.SkillGrants)
        {
            if (skillGrant.Domain == "magical-group")
            {
                var allocations = selection.SkillGroupGrants ?? [];
                if (allocations.Count != skillGrant.Count)
                {
                    diagnostics.Add(SkillGrantCount("magical-group", skillGrant.Count, allocations.Count, path: "magicResonance.skillGroupGrants"));
                }

                foreach (var allocation in allocations)
                {
                    if (!catalog.SkillGroups.TryGetValue(allocation.SkillGroupId, out var group))
                    {
                        diagnostics.Add(Unknown(allocation.SkillGroupId, catalog, "magicResonance.skillGroupGrants"));
                    }
                    else if (group.Id is not ("sorcery" or "conjuring" or "enchanting"))
                    {
                        diagnostics.Add(Error("magic.skill-grant.domain", "magicResonance.skillGroupGrants",
                            [allocation.SkillGroupId], group.Source,
                            new Dictionary<string, string> { ["domain"] = "magical-group" },
                            "The granted group must be a Magical skill group."));
                    }
                }

                if (allocations.Select(item => item.SkillGroupId).Distinct(StringComparer.Ordinal).Count() != allocations.Count)
                {
                    diagnostics.Add(SkillGrantDuplicate("magical-group"));
                }

                continue;
            }

            var skillAllocations = selection.SkillGrants ?? [];
            if (skillAllocations.Count != skillGrant.Count)
            {
                diagnostics.Add(SkillGrantCount(skillGrant.Domain, skillGrant.Count, skillAllocations.Count, "magicResonance.skillGrants"));
            }

            foreach (var allocation in skillAllocations)
            {
                if (!catalog.Skills.TryGetValue(allocation.SkillId, out var skill))
                {
                    diagnostics.Add(Unknown(allocation.SkillId, catalog, "magicResonance.skillGrants"));
                }
                else if (skill.Domain != skillGrant.Domain)
                {
                    diagnostics.Add(Error("magic.skill-grant.domain", "magicResonance.skillGrants",
                        [allocation.SkillId], skill.Source,
                        new Dictionary<string, string> { ["domain"] = skillGrant.Domain },
                        $"The granted skill must be a {skillGrant.Domain} skill."));
                }
            }

            if (skillAllocations.Select(item => item.SkillId).Distinct(StringComparer.Ordinal).Count() != skillAllocations.Count)
            {
                diagnostics.Add(SkillGrantDuplicate(skillGrant.Domain));
            }
        }
    }

    private static void EvaluateFormulae(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        MagicResonancePathGrant grant,
        int magicValue,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var aspect = path.Kind == CreationPathKind.AspectedMagician && selection.AspectedValueId is not null
            ? catalog.AspectedValues.GetValueOrDefault(selection.AspectedValueId)
            : null;
        var canSelectSpells = path.Kind is CreationPathKind.Magician or CreationPathKind.MysticAdept
            || aspect?.CanSelectSpells == true;
        var canSelectRituals = path.Kind is CreationPathKind.Magician or CreationPathKind.MysticAdept
            || aspect?.CanSelectRituals == true;
        var canSelectPreparations = path.Kind is CreationPathKind.Magician or CreationPathKind.MysticAdept
            || aspect?.CanSelectPreparations == true;

        var spells = selection.Spells ?? [];
        var rituals = selection.Rituals ?? [];
        var preparations = selection.Preparations ?? [];

        if (spells.Count > 0 && !canSelectSpells)
        {
            diagnostics.Add(FormulaNotAllowed("spells", path.Source));
        }

        if (rituals.Count > 0 && !canSelectRituals)
        {
            diagnostics.Add(FormulaNotAllowed("rituals", path.Source));
        }

        if (preparations.Count > 0 && !canSelectPreparations)
        {
            diagnostics.Add(FormulaNotAllowed("preparations", path.Source));
        }

        foreach (var spell in spells)
        {
            if (!catalog.Spells.TryGetValue(spell.SpellId, out var definition))
            {
                diagnostics.Add(Unknown(spell.SpellId, catalog, "magicResonance.spells"));
            }
            else
            {
                if (definition.Parameterized && string.IsNullOrWhiteSpace(spell.Parameter))
                {
                    diagnostics.Add(Error("magic.formula.parameter.required", $"magicResonance.spells[{spell.SpellId}].parameter",
                        [spell.SpellId], definition.Source, new Dictionary<string, string>(),
                        "Complete the required parameter for this spell."));
                }

                if (spell.Parameter is { Length: > MaxTextLength })
                {
                    diagnostics.Add(TextTooLong(definition.Source));
                }
            }
        }

        foreach (var ritual in rituals)
        {
            if (!catalog.Rituals.TryGetValue(ritual.RitualId, out _))
            {
                diagnostics.Add(Unknown(ritual.RitualId, catalog, "magicResonance.rituals"));
            }
        }

        foreach (var preparation in preparations)
        {
            if (!catalog.Spells.TryGetValue(preparation.SpellId, out _))
            {
                diagnostics.Add(Unknown(preparation.SpellId, catalog, "magicResonance.preparations"));
            }

            if (preparation.Trigger is not ("command" or "contact" or "time"))
            {
                diagnostics.Add(Error("magic.preparation.trigger.invalid", "magicResonance.preparations.trigger",
                    [], path.Source, new Dictionary<string, string> { ["trigger"] = CharacterCreationDiagnosticFactory.Bounded(preparation.Trigger) },
                    "Choose a Command, Contact, or Time trigger."));
            }

            if (preparation.Trigger == "time" && preparation.DelayHours is null or <= 0)
            {
                diagnostics.Add(Error("magic.preparation.delay.required", "magicResonance.preparations.delayHours",
                    [], path.Source, new Dictionary<string, string>(),
                    "A Time-triggered preparation requires a positive delay in hours."));
            }
        }

        var grantedCount = spells.Count(item => item.Granted)
            + rituals.Count(item => item.Granted)
            + preparations.Count(item => item.Granted);
        if (grantedCount > grant.FormulaGrants)
        {
            diagnostics.Add(Error("magic.formula.grants-exceeded", "magicResonance", [], path.Source,
                new Dictionary<string, string>
                {
                    ["available"] = grant.FormulaGrants.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["spent"] = grantedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce granted formulae to the priority grant."));
        }

        var magic = grant.AttributeRating + magicValue;
        var cap = magic * 2;
        if (spells.Count > cap)
        {
            diagnostics.Add(FormulaCapExceeded("spells", cap, spells.Count, path.Source));
        }

        if (rituals.Count > cap)
        {
            diagnostics.Add(FormulaCapExceeded("rituals", cap, rituals.Count, path.Source));
        }

        if (preparations.Count > cap)
        {
            diagnostics.Add(FormulaCapExceeded("preparations", cap, preparations.Count, path.Source));
        }
    }

    private static void EvaluatePowerPoints(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        MagicResonancePathGrant grant,
        int magicValue,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var powers = selection.AdeptPowers ?? [];
        var canUsePowers = path.Kind is CreationPathKind.Adept or CreationPathKind.MysticAdept;
        if (powers.Count > 0 && !canUsePowers)
        {
            diagnostics.Add(Error("magic.power.not-allowed", "magicResonance.adeptPowers", [],
                path.Source, new Dictionary<string, string>(),
                "This path does not select adept powers."));
        }

        if (selection.PurchasedPowerPoints is not null && path.Kind != CreationPathKind.MysticAdept)
        {
            diagnostics.Add(Error("magic.power-points.purchase.not-allowed", "magicResonance.purchasedPowerPoints", [],
                path.Source, new Dictionary<string, string>(),
                "Only a mystic adept purchases Power Points with Karma."));
        }

        if (!canUsePowers)
        {
            return;
        }

        var magic = grant.AttributeRating + magicValue;
        var totalCost = powers.Sum(item => PowerCost(catalog, item));
        if (path.Kind == CreationPathKind.Adept)
        {
            if (totalCost > magic)
            {
                diagnostics.Add(Error("magic.power-points.exceeded", "magicResonance.adeptPowers", [], path.Source,
                    new Dictionary<string, string>
                    {
                        ["available"] = magic.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["spent"] = totalCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce adept power selections to the Power Points granted by Magic."));
            }
        }
        else
        {
            var purchased = selection.PurchasedPowerPoints ?? 0;
            if (purchased < 0 || purchased > magic)
            {
                diagnostics.Add(Error("magic.power-points.purchase.exceeded", "magicResonance.purchasedPowerPoints", [],
                    path.Source,
                    new Dictionary<string, string>
                    {
                        ["available"] = magic.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["spent"] = purchased.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Buy no more Power Points than the character's Magic."));
            }

            if (totalCost > purchased)
            {
                diagnostics.Add(Error("magic.power-points.exceeded", "magicResonance.adeptPowers", [], path.Source,
                    new Dictionary<string, string>
                    {
                        ["available"] = purchased.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["spent"] = totalCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce adept power selections to the purchased Power Points."));
            }
        }

        foreach (var power in powers)
        {
            if (!catalog.AdeptPowers.TryGetValue(power.PowerId, out var definition))
            {
                diagnostics.Add(Unknown(power.PowerId, catalog, "magicResonance.adeptPowers"));
                continue;
            }

            if (definition.Ranked)
            {
                var maxRank = definition.MaxRank ?? magic;
                var rank = power.Rank ?? 0;
                if (rank < 1 || rank > maxRank)
                {
                    diagnostics.Add(Error("magic.power.rank.invalid", $"magicResonance.adeptPowers[{power.PowerId}].rank",
                        [power.PowerId], definition.Source,
                        new Dictionary<string, string> { ["maximum"] = maxRank.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        "Use a rank from 1 through the power maximum."));
                }
            }
            else if (power.Rank is not null)
            {
                diagnostics.Add(Error("magic.power.rank.not-allowed", $"magicResonance.adeptPowers[{power.PowerId}].rank",
                    [power.PowerId], definition.Source, new Dictionary<string, string>(),
                    "This adept power is not ranked."));
            }

            if (definition.Parameterized && string.IsNullOrWhiteSpace(power.Parameter))
            {
                diagnostics.Add(Error("magic.power.parameter.required", $"magicResonance.adeptPowers[{power.PowerId}].parameter",
                    [power.PowerId], definition.Source, new Dictionary<string, string>(),
                    "Complete the required parameter for this adept power."));
            }

            if (power.Parameter is { Length: > MaxTextLength })
            {
                diagnostics.Add(TextTooLong(definition.Source));
            }
        }
    }

    private static decimal PowerCost(RulesetCatalog catalog, AdeptPowerSelection power)
    {
        if (!catalog.AdeptPowers.TryGetValue(power.PowerId, out var definition))
        {
            return 0;
        }

        var rank = definition.Ranked ? (power.Rank ?? 1) : 1;
        return definition.PowerPointCostByRank is { } byRank
            ? byRank.GetValueOrDefault(rank, definition.PowerPointCost * rank)
            : definition.PowerPointCost * rank;
    }

    private static void EvaluateComplexForms(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        MagicResonancePathGrant grant,
        int resonanceValue,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var forms = selection.ComplexForms ?? [];
        if (path.Kind != CreationPathKind.Technomancer)
        {
            if (forms.Count > 0)
            {
                diagnostics.Add(Error("magic.complex-form.not-allowed", "magicResonance.complexForms", [],
                    path.Source, new Dictionary<string, string>(),
                    "Only a technomancer selects complex forms."));
            }

            return;
        }

        foreach (var form in forms)
        {
            if (!catalog.ComplexForms.TryGetValue(form.ComplexFormId, out _))
            {
                diagnostics.Add(Unknown(form.ComplexFormId, catalog, "magicResonance.complexForms"));
            }
        }

        var grantedCount = forms.Count(item => item.Granted);
        if (grantedCount > grant.ComplexFormGrants)
        {
            diagnostics.Add(Error("magic.complex-form.grants-exceeded", "magicResonance.complexForms", [], path.Source,
                new Dictionary<string, string>
                {
                    ["available"] = grant.ComplexFormGrants.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["spent"] = grantedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce granted complex forms to the priority grant."));
        }

        var resonance = grant.AttributeRating + resonanceValue;
        var logic = NaturalLogicValue(catalog, document);
        var cap = Math.Min(logic, resonance * 2);
        if (forms.Count > cap)
        {
            diagnostics.Add(Error("magic.complex-form.cap-exceeded", "magicResonance.complexForms", [], path.Source,
                new Dictionary<string, string>
                {
                    ["available"] = cap.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["spent"] = forms.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Total known complex forms cannot exceed the lower of Logic and Resonance times two."));
        }
    }

    private static void EvaluateMentorSpirit(
        RulesetCatalog catalog,
        CreationPathDefinition path,
        MagicResonanceSelection selection,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var mentor = selection.MentorSpirit;
        var hasMentorQuality = (document.Qualities ?? []).Any(item => item.QualityId == "mentor-spirit");
        var isAwakened = string.Equals(path.AttributeId, "magic", StringComparison.Ordinal);
        if (mentor is null)
        {
            if (hasMentorQuality && isAwakened)
            {
                diagnostics.Add(Error("magic.mentor.required", "magicResonance.mentorSpirit", [],
                    path.Source, new Dictionary<string, string>(),
                    "The Mentor Spirit quality requires choosing a mentor archetype."));
            }

            return;
        }

        if (!hasMentorQuality)
        {
            diagnostics.Add(Error("magic.mentor.requires-quality", "magicResonance.mentorSpirit",
                ["mentor-spirit"], path.Source, new Dictionary<string, string>(),
                "Select the 5-Karma Mentor Spirit quality before choosing a mentor."));
        }

        if (!isAwakened)
        {
            diagnostics.Add(Error("magic.mentor.requires-awakened", "magicResonance.mentorSpirit", [],
                path.Source, new Dictionary<string, string>(),
                "A mentor spirit requires an Awakened creation path."));
        }

        if (!catalog.MentorSpirits.TryGetValue(mentor.MentorSpiritId, out var definition))
        {
            diagnostics.Add(Unknown(mentor.MentorSpiritId, catalog, "magicResonance.mentorSpirit"));
        }
        else
        {
            if (definition.Parameterized && string.IsNullOrWhiteSpace(mentor.Choice))
            {
                diagnostics.Add(Error("magic.mentor.choice.required", "magicResonance.mentorSpirit.choice",
                    [mentor.MentorSpiritId], definition.Source, new Dictionary<string, string>(),
                    "Complete the required choice for this mentor archetype."));
            }

            if (mentor.Choice is { Length: > MaxTextLength })
            {
                diagnostics.Add(TextTooLong(definition.Source));
            }
        }
    }

    private static int NaturalLogicValue(RulesetCatalog catalog, CharacterCreationDraftDocument document)
    {
        if (document.Metatype is null || document.Attributes is null)
        {
            return 0;
        }

        if (!catalog.Metatypes.TryGetValue(document.Metatype.MetatypeId, out var metatype))
        {
            return 0;
        }

        if (!metatype.Attributes.TryGetValue("logic", out var range))
        {
            return 0;
        }

        var values = document.Attributes.Values;
        var allocated = values.TryGetValue("logic", out var logic) ? Math.Max(0, logic) : 0;
        return range.Minimum + allocated;
    }

    private static CharacterCreationDiagnostic SkillGrantCount(
        string domain,
        int expected,
        int actual,
        string path)
    {
        var source = new SourceCitation("sr5-core", 65, 67);
        return Error("magic.skill-grant.count", path, [], source,
            new Dictionary<string, string>
            {
                ["domain"] = domain,
                ["expected"] = expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["actual"] = actual.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            $"The {domain} priority grant expects exactly {expected} selection{(expected == 1 ? string.Empty : "s")}.");
    }

    private static CharacterCreationDiagnostic SkillGrantDuplicate(string domain) =>
        Error("magic.skill-grant.duplicate", "magicResonance.skillGrants", [], new SourceCitation("sr5-core", 65, 67),
            new Dictionary<string, string> { ["domain"] = domain },
            "Each priority-granted skill selection must be distinct.");

    private static CharacterCreationDiagnostic FormulaNotAllowed(string kind, SourceCitation source) =>
        Error($"magic.formula.not-allowed.{kind}", "magicResonance", [], source, new Dictionary<string, string>(),
            "This creation path cannot select formulae of this kind.");

    private static CharacterCreationDiagnostic FormulaCapExceeded(
        string kind,
        int cap,
        int actual,
        SourceCitation source) =>
        Error($"magic.formula.cap-exceeded.{kind}", "magicResonance", [], source,
            new Dictionary<string, string>
            {
                ["maximum"] = cap.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["actual"] = actual.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            $"No more than {cap} {kind} may be known at creation.");

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, messageArguments, resolution);

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, resolution);

    private static CharacterCreationDiagnostic Unknown(string? id, RulesetCatalog catalog, string field)
    {
        var source = catalog.Sources["sr5-core"];
        return CharacterCreationDiagnosticFactory.Unknown(Step, id, field, new SourceCitation(source.Id, 65, 67));
    }

    private static CharacterCreationDiagnostic TextTooLong(SourceCitation source) =>
        CharacterCreationDiagnosticFactory.TextTooLong(Step, "magicResonance", source);
}
