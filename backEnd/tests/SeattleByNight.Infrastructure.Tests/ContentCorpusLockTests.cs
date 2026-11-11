using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.Tests;

// WF-22: publish, delete and draft saves are check-then-write over the whole
// corpus. With two storytellers they have to take turns, or a delete's "nothing
// points at this" can be answered just before a publish starts pointing at it.
public sealed class ContentCorpusLockTests : PlaythroughHarness
{
    [Fact]
    public async Task A_draft_save_waits_while_another_storyteller_holds_the_corpus()
    {
        var lockProbe = Guid.NewGuid();
        var holding = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        await using var first = Provider.CreateAsyncScope();
        var held = first.ServiceProvider.GetRequiredService<IGameContentStore>().RunExclusiveAsync(
            async _ =>
            {
                holding.SetResult();
                await release.Task;
                return 0;
            });
        await holding.Task;

        await using var second = Provider.CreateAsyncScope();
        var save = second.ServiceProvider.GetRequiredService<IGameContentStore>().SaveDraftAsync(
            GameContentKind.Test, lockProbe, "Lock probe", "{}", UserId);

        var winner = await Task.WhenAny(save, Task.Delay(TimeSpan.FromMilliseconds(500)));
        Assert.NotSame(save, winner);

        release.SetResult();
        await held;
        var saved = await save.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(lockProbe, saved.ContentKey);
    }

    [Fact]
    public async Task The_lock_is_reentrant_so_a_publish_can_save_inside_it()
    {
        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();

        var saved = await store.RunExclusiveAsync(ct =>
            store.SaveDraftAsync(GameContentKind.Test, Guid.NewGuid(), "Nested", "{}", UserId, ct));

        Assert.NotNull(await store.FindAsync(GameContentKind.Test, saved.ContentKey));
    }
}
