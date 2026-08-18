using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.RoomChat;

public sealed class RoomChatStore : IRoomChatStore
{
    private readonly SeattleByNightDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public RoomChatStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<SendRoomMessageOutcome?> SendMessageAsync(
        Guid userId,
        string content,
        ChatMessageType type,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the user's active play-session row so chat, movement, and expiration
        // serialize on the same authoritative state before resolving room and expiry.
        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (session is null || session.ExpiresAtUtc <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var character = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.Id == session.CharacterId && c.LifecycleState == CharacterLifecycleState.Finalized)
            .Select(c => new { c.Id, c.Name, c.CurrentRoomId })
            .FirstOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        session.LastActivityUtc = now;
        session.ExpiresAtUtc = now + idleTimeout;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RoomId = character.CurrentRoomId,
            CharacterId = character.Id,
            Type = type,
            Content = content,
            CreatedAtUtc = now
        };

        _dbContext.ChatMessages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SendRoomMessageOutcome(
            new RoomMessage(message.Id, character.CurrentRoomId, character.Id, character.Name, content, type, now),
            session.ExpiresAtUtc);
    }
}
