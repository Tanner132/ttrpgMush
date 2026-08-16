using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.RoomChat;

public sealed class RoomChatStore : IRoomChatStore
{
    private readonly SeattleByNightDbContext _dbContext;

    public RoomChatStore(SeattleByNightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SendRoomMessageOutcome?> SendMessageAsync(
        Guid userId,
        string content,
        DateTimeOffset now,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the user's active play-session row so chat, movement, and expiration
        // serialize on the same authoritative state before resolving room and expiry.
        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null || session.ExpiresAtUtc <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var character = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.Id == session.CharacterId)
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
            Content = content,
            CreatedAtUtc = now
        };

        _dbContext.ChatMessages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SendRoomMessageOutcome(
            new RoomMessage(message.Id, character.CurrentRoomId, character.Id, character.Name, content, now),
            session.ExpiresAtUtc);
    }
}
