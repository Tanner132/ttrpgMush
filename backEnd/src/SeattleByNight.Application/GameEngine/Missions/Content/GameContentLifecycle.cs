using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// Why a hard delete was refused, in the terms an admin can act on.
public sealed record GameContentDeleteCheck(bool CanDelete, string? Reason)
{
    public static GameContentDeleteCheck Allowed() => new(true, null);

    public static GameContentDeleteCheck Refused(string reason) => new(false, reason);
}

// Historical rows that name a definition. Deleting content those rows point at
// would leave a character's history dangling, which is the one thing this
// milestone promises never happens.
public sealed record GameContentUsage(int Count, string Description);

public interface IGameContentUsageReader
{
    Task<GameContentUsage> CountHistoricalReferencesAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default);
}

// Milestone 7 section 5: the two ways content leaves play, and the difference
// between them.
//
// RETIRE is the workhorse and is always allowed. A retired definition stays in
// the served document — instances already running still resolve what they were
// built from — but nothing new is offered it. It is instant and reversible:
// publishing it again puts it straight back.
//
// HARD DELETE erases the record, so it is only ever offered when there is no
// record to break: nothing historical points at the definition, and the corpus
// still loads without it. The gate for the second half is the same
// GameContentLoader that guards a publish, run over the corpus with the
// definition removed — a reference that would dangle shows up as a load error
// naming exactly what still points at it.
public sealed class GameContentLifecycle(
    IGameContentStore store,
    IGameContentProvider content,
    IGameContentUsageReader usage)
{
    public async Task<GameContentDeleteCheck> CanDeleteAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default)
    {
        var definition = await store.FindAsync(kind, contentKey, cancellationToken);
        if (definition is null)
        {
            return GameContentDeleteCheck.Refused(
                $"No {kind.ToString().ToLowerInvariant()} named '{contentKey}' exists.");
        }

        // A draft has never been live, so nothing can have referenced it and
        // it is not in the corpus to break.
        if (definition.Status == GameContentStatus.Draft)
        {
            return GameContentDeleteCheck.Allowed();
        }

        var referenced = await usage.CountHistoricalReferencesAsync(kind, contentKey, cancellationToken);
        if (referenced.Count > 0)
        {
            return GameContentDeleteCheck.Refused(
                $"'{contentKey}' is referenced by {referenced.Count} {referenced.Description}. "
                    + "Retire it instead — that takes it out of play and leaves the record intact.");
        }

        var served = await store.ListServedAsync(cancellationToken);
        var withoutIt = served
            .Where(record => record.Id != definition.Id)
            .Select(record => (record.Kind, record.PublishedJson!));

        var validation = GameContentPublisher.ValidateCorpus(withoutIt);
        return validation.IsSuccess
            ? GameContentDeleteCheck.Allowed()
            : GameContentDeleteCheck.Refused(
                $"Other content still points at '{contentKey}': {validation.Error} "
                    + "Retire it instead, or remove the references first.");
    }

    // Always allowed for anything that has been live. The definition stays in
    // the document so running instances keep resolving it; the retirement flag
    // is what stops it being offered to anyone new.
    public async Task<GameContentPublishResult> RetireAsync(
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

        if (definition.Status == GameContentStatus.Draft)
        {
            return GameContentPublishResult.Failure(
                $"'{contentKey}' has never been published, so there is nothing to retire. Delete it instead.");
        }

        if (definition.Status == GameContentStatus.Retired)
        {
            return GameContentPublishResult.Success();
        }

        await store.MarkRetiredAsync(definition.Id, actorUserId, cancellationToken);
        await content.ReloadAsync(cancellationToken);
        return GameContentPublishResult.Success();
    }

    public async Task<GameContentPublishResult> DeleteAsync(
        GameContentKind kind,
        string contentKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var check = await CanDeleteAsync(kind, contentKey, cancellationToken);
        if (!check.CanDelete)
        {
            return GameContentPublishResult.Failure(check.Reason!);
        }

        await store.DeleteAsync(kind, contentKey, actorUserId, cancellationToken);
        await content.ReloadAsync(cancellationToken);
        return GameContentPublishResult.Success();
    }
}
