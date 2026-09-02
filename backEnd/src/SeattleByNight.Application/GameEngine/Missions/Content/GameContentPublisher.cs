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
public sealed class GameContentPublisher(IGameContentStore store, IGameContentProvider content)
{
    // Dry run: would publishing this draft produce a loadable corpus? Same
    // composition, same checks, no writes.
    public async Task<GameContentPublishResult> ValidateDraftAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default)
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
        return ValidateCorpus(served.Select(record => (record.Kind, record.PublishedJson!)));
    }

    public async Task<GameContentPublishResult> PublishAsync(
        GameContentKind kind,
        string contentKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var definition = await store.FindAsync(kind, contentKey, cancellationToken);
        if (definition is null)
        {
            return GameContentPublishResult.Failure(
                $"No {kind.ToString().ToLowerInvariant()} named '{contentKey}' exists.");
        }

        var validation = await ValidateAsync(definition, cancellationToken);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        await store.MarkPublishedAsync(definition.Id, actorUserId, cancellationToken);
        // The provider serves a cached document; a publish that did not reach
        // the running game would be a publish that did nothing.
        await content.ReloadAsync(cancellationToken);
        return GameContentPublishResult.Success();
    }

    private async Task<GameContentPublishResult> ValidateAsync(
        GameContentDefinitionRecord candidate, CancellationToken cancellationToken)
    {
        var served = await store.ListServedAsync(cancellationToken);
        var corpus = served
            .Where(record => record.Id != candidate.Id)
            .Select(record => (record.Kind, record.PublishedJson!))
            .Append((candidate.Kind, candidate.DraftJson));

        return ValidateCorpus(corpus);
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
