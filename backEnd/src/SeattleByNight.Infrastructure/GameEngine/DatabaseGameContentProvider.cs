using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.GameEngine;

// Milestone 7 (§50): serves the game content the database holds, composed
// from every published definition and validated by the same GameContentLoader
// that validates the embedded bundle. Replaces EmbeddedGameContentProvider as
// the provider the running game reads through; the engines and appliers that
// consume IGameContentProvider are unchanged.
//
// A singleton holding an immutable cached document: reads on the action path
// must not hit the database, and swapping the whole document at once means a
// publish is atomic from a player's point of view — no request ever sees half
// of one content set and half of another.
public sealed class DatabaseGameContentProvider(IServiceScopeFactory scopes) : IGameContentProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private GameContentDocument? cached;

    // The composed document. Loaded eagerly at startup (see Program.cs) so a
    // broken content set fails boot; the lazy fallback covers compositions
    // that never run the startup path, such as the playthrough harness.
    public GameContentDocument Current =>
        Volatile.Read(ref cached) ?? LoadBlocking();

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Volatile.Write(ref cached, await ComposeAsync(cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<GameContentDocument> ComposeAsync(CancellationToken cancellationToken)
    {
        // The store is scoped (it owns a DbContext) and this provider is not,
        // so every load runs in its own scope.
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var served = await store.ListServedAsync(cancellationToken);
        var retired = await store.ListRetiredKeysAsync(cancellationToken);

        var document = GameContentComposer.ComposeAndLoad(
            served.Select(definition => (definition.Kind, definition.PublishedJson!)),
            StampVersion(served));

        return StampRetirement(document, retired);
    }

    // Milestone 7 section 5: retirement is store metadata, not something the
    // loader parses, so it is stamped onto the composed definitions afterwards.
    // Retired content stays IN the document — an instance already running has
    // to keep resolving what it was built from — and the flag is what the
    // offer-side gates read.
    private static GameContentDocument StampRetirement(
        GameContentDocument document,
        IReadOnlyDictionary<GameContentKind, IReadOnlySet<string>> retired)
    {
        if (retired.Count == 0)
        {
            return document;
        }

        IReadOnlySet<string> KeysOf(GameContentKind kind) =>
            retired.TryGetValue(kind, out var keys) ? keys : new HashSet<string>();

        var encounters = KeysOf(GameContentKind.Encounter);
        var missions = KeysOf(GameContentKind.Mission);
        var scenes = KeysOf(GameContentKind.Scene);
        var templates = KeysOf(GameContentKind.NpcTemplate);

        return document with
        {
            Encounters = document.Encounters
                .Select(entry => entry with { IsRetired = encounters.Contains(entry.Id) })
                .ToArray(),
            Missions = document.Missions
                // A job whose site has been taken out of play is itself out of
                // play: retiring an encounter retires everything that runs in
                // it, without an admin having to chase the list down.
                .Select(entry => entry with
                {
                    IsRetired = missions.Contains(entry.Id) || encounters.Contains(entry.EncounterId),
                })
                .ToArray(),
            Scenes = document.Scenes
                .Select(entry => entry with { IsRetired = scenes.Contains(entry.Id) })
                .ToArray(),
            NpcTemplates = document.NpcTemplates
                .Select(entry => entry with { IsRetired = templates.Contains(entry.TemplateId) })
                .ToArray(),
        };
    }

    // The served document's version is the newest PUBLISH time in it, so the
    // builder (and an operator reading a log line) can tell which revision the
    // game is running without diffing payloads. UpdatedAtUtc would move when a
    // draft is saved on a published row, which changes nothing about what the
    // game is serving — a revision that moves without a publish is a revision
    // nobody can trust.
    private static string StampVersion(IReadOnlyList<GameContentDefinitionRecord> served)
    {
        var newest = served
            .Select(definition => definition.PublishedAtUtc)
            .Where(published => published is not null)
            .DefaultIfEmpty(null)
            .Max();

        return newest is { } stamp
            ? stamp.UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)
            : "empty";
    }

    private GameContentDocument LoadBlocking()
    {
        // Off the current thread so the one-time synchronous load cannot
        // deadlock against a caller's synchronization context. Callers after
        // the first read the cached field and never reach here.
        Task.Run(() => ReloadAsync(CancellationToken.None)).GetAwaiter().GetResult();
        return Volatile.Read(ref cached)!;
    }
}
