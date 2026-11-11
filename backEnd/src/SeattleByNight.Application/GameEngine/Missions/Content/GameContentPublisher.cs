using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// The outcome of a publish attempt or a dry-run validation. Failures carry
// the loader's own message, which names the offending definition and what is
// wrong with it — the builder shows it verbatim.
public sealed record GameContentPublishResult(bool IsSuccess, string? Error)
{
    public static GameContentPublishResult Success() => new(true, null);

    public static GameContentPublishResult Failure(string error) => new(false, error);
}

// One definition named by kind and id, for publishing several together.
public sealed record GameContentRef(GameContentKind Kind, Guid ContentKey);

// Milestone 7: the draft → validate → publish gate. Publishing composes the
// candidate corpus — everything currently published, with this definition's
// draft swapped in — and runs the full GameContentLoader validation suite
// over it. Invalid content cannot be published, so the game never serves a
// document that would have failed startup validation.
//
// Validation is whole-corpus, not per-definition, because the checks that
// matter most are cross-references: a mission naming an encounter, an
// objective naming an item that encounter declares, a scene effect naming
// a mission. A definition is only publishable together with everything it
// points at.
public sealed class GameContentPublisher(
    IGameContentStore store,
    IGameContentProvider content,
    IJobSiteMapReader siteMap)
{
    // Dry run: would publishing this draft produce a loadable corpus? Same
    // composition, same checks, no writes.
    public async Task<GameContentPublishResult> ValidateDraftAsync(
        GameContentKind kind, Guid contentKey, CancellationToken cancellationToken = default)
    {
        var definition = await store.FindAsync(kind, contentKey, cancellationToken);
        return definition is null
            ? GameContentPublishResult.Failure(
                $"No {kind.ToString().ToLowerInvariant()} named '{contentKey}' exists.")
            : await ValidateAsync(definition, cancellationToken);
    }

    // Validates the whole published corpus as it stands — what the provider
    // is about to load, and the check the seeder's import has to pass.
    public async Task<GameContentPublishResult> ValidatePublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var served = await store.ListServedAsync(cancellationToken);
        return await ValidateWithMapAsync(
            served.Select(record => (record.Kind, record.PublishedJson!)), cancellationToken);
    }

    public async Task<GameContentPublishResult> PublishAsync(
        GameContentKind kind,
        Guid contentKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        // WF-22: validate and promote under the corpus lock, so what is
        // promoted is exactly what was validated against exactly the corpus
        // it will join.
        var result = await store.RunExclusiveAsync(
            async ct =>
            {
                var definition = await store.FindAsync(kind, contentKey, ct);
                if (definition is null)
                {
                    return GameContentPublishResult.Failure(
                        $"No {kind.ToString().ToLowerInvariant()} named '{contentKey}' exists.");
                }

                var validation = await ValidateAsync(definition, ct);
                if (!validation.IsSuccess)
                {
                    return validation;
                }

                await store.MarkPublishedAsync(definition.Id, actorUserId, ct);
                return GameContentPublishResult.Success();
            },
            cancellationToken);

        // The provider serves a cached document; a publish that did not reach
        // the running game would be a publish that did nothing. Reloaded after
        // the commit, or it would read the corpus as it was.
        if (result.IsSuccess)
        {
            await content.ReloadAsync(cancellationToken);
        }

        return result;
    }

    // WF-28: a job is several definitions that point at each other — the job
    // names its site and the conversations its events open, and the Johnson's
    // conversation names the job — so none of them can pass the gate alone
    // the first time. A set is validated as one candidate corpus (everything
    // published, with every draft in the set swapped in) and promoted
    // together, or not at all.
    public async Task<GameContentPublishResult> ValidateSetAsync(
        IReadOnlyList<GameContentRef> items, CancellationToken cancellationToken = default)
    {
        var (candidates, missing) = await FindSetAsync(items, cancellationToken);
        return missing ?? await ValidateAsync(candidates, cancellationToken);
    }

    public async Task<GameContentPublishResult> PublishSetAsync(
        IReadOnlyList<GameContentRef> items,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await store.RunExclusiveAsync(
            async ct =>
            {
                var (candidates, missing) = await FindSetAsync(items, ct);
                if (missing is not null)
                {
                    return missing;
                }

                var validation = await ValidateAsync(candidates, ct);
                if (!validation.IsSuccess)
                {
                    return validation;
                }

                foreach (var candidate in candidates)
                {
                    await store.MarkPublishedAsync(candidate.Id, actorUserId, ct);
                }

                return GameContentPublishResult.Success();
            },
            cancellationToken);

        if (result.IsSuccess)
        {
            await content.ReloadAsync(cancellationToken);
        }

        return result;
    }

    private async Task<(IReadOnlyList<GameContentDefinitionRecord> Candidates, GameContentPublishResult? Missing)>
        FindSetAsync(IReadOnlyList<GameContentRef> items, CancellationToken cancellationToken)
    {
        var candidates = new List<GameContentDefinitionRecord>(items.Count);
        foreach (var item in items.Distinct())
        {
            var definition = await store.FindAsync(item.Kind, item.ContentKey, cancellationToken);
            if (definition is null)
            {
                return ([], GameContentPublishResult.Failure(
                    $"No {item.Kind.ToString().ToLowerInvariant()} named '{item.ContentKey}' exists."));
            }

            candidates.Add(definition);
        }

        return candidates.Count == 0
            ? ([], GameContentPublishResult.Failure("Nothing was named to publish."))
            : (candidates, null);
    }

    private Task<GameContentPublishResult> ValidateAsync(
        GameContentDefinitionRecord candidate, CancellationToken cancellationToken) =>
        ValidateAsync([candidate], cancellationToken);

    private async Task<GameContentPublishResult> ValidateAsync(
        IReadOnlyList<GameContentDefinitionRecord> candidates, CancellationToken cancellationToken)
    {
        var replaced = candidates.Select(candidate => candidate.Id).ToHashSet();
        var served = await store.ListServedAsync(cancellationToken);
        var corpus = served
            .Where(record => !replaced.Contains(record.Id))
            .Select(record => (record.Kind, record.PublishedJson!))
            .Concat(candidates.Select(candidate => (candidate.Kind, candidate.DraftJson)));

        return await ValidateWithMapAsync(corpus, cancellationToken);
    }

    // WF-25: the loader's checks, then the job-site checks only the map can
    // answer (are the site's rooms Private map rooms, and connected).
    private async Task<GameContentPublishResult> ValidateWithMapAsync(
        IEnumerable<(GameContentKind Kind, string Json)> corpus, CancellationToken cancellationToken)
    {
        GameContentDocument document;
        try
        {
            document = GameContentComposer.ComposeAndLoad(corpus, "validation");
        }
        catch (GameContentException exception)
        {
            return GameContentPublishResult.Failure(exception.Message);
        }

        try
        {
            JobSiteMapValidator.Validate(document, await siteMap.ReadAsync(cancellationToken));
            return GameContentPublishResult.Success();
        }
        catch (GameContentException exception)
        {
            return GameContentPublishResult.Failure(exception.Message);
        }
    }

    // Public because the retire/delete lifecycle asks the same question of a
    // corpus with a definition removed: would what is left still load?
    public static GameContentPublishResult ValidateCorpus(
        IEnumerable<(GameContentKind Kind, string Json)> corpus)
    {
        try
        {
            // The version stamp is cosmetic during validation; the real one is
            // stamped when the provider composes the live document.
            GameContentComposer.ComposeAndLoad(corpus, "validation");
            return GameContentPublishResult.Success();
        }
        catch (GameContentException exception)
        {
            // The loader stops at the first problem it finds. That is a hard
            // gate with a precise message, which is what publishing needs;
            // reporting every error at once would mean teaching the loader to
            // accumulate instead of throw, and is a later refinement.
            return GameContentPublishResult.Failure(exception.Message);
        }
    }
}
