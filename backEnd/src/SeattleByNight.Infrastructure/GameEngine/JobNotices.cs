using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

public sealed class JobNotices(
    SeattleByNightDbContext db,
    IGameMessageBroadcaster broadcaster,
    TimeProvider timeProvider) : IJobNotices
{
    public async Task TellAsync(
        IReadOnlyCollection<Guid> characterIds, string text, CancellationToken cancellationToken = default)
    {
        if (characterIds.Count == 0)
        {
            return;
        }

        var recipients = await db.Characters
            .AsNoTracking()
            .Where(character => characterIds.Contains(character.Id))
            .Select(character => new { character.UserId, character.CurrentRoomId })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            await broadcaster.SendToUserAsync(
                recipient.UserId,
                new RoomMessage(
                    Guid.NewGuid(), recipient.CurrentRoomId, Guid.Empty, string.Empty, text,
                    ChatMessageType.Narration, timeProvider.GetUtcNow()),
                cancellationToken);
        }
    }
}
