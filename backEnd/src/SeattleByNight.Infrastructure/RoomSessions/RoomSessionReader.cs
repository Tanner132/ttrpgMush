using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.RoomSessions;

public sealed class RoomSessionReader : IRoomSessionReader
{
    private readonly SeattleByNightDbContext _dbContext;
    private readonly ICombatTracker _combatTracker;
    private readonly IRunAccessPolicy _runAccess;

    public RoomSessionReader(
        SeattleByNightDbContext dbContext, ICombatTracker combatTracker, IRunAccessPolicy runAccess)
    {
        _dbContext = dbContext;
        _combatTracker = combatTracker;
        _runAccess = runAccess;
    }

    public async Task<RoomSession?> GetByPlaySessionIdAsync(
        Guid playSessionId,
        string? olderMessagesCursor,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.PlaySessions
            .AsNoTracking()
            .Where(s => s.Id == playSessionId)
            .Select(s => new { s.Id, s.CharacterId, s.StartAtUtc, s.EndedAtUtc, s.ExpiresAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        var character = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.Id == session.CharacterId && c.LifecycleState == CharacterLifecycleState.Finalized)
            .Select(c => new { c.Id, c.Name, c.CurrentRoomId })
            .FirstOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            return null;
        }

        var room = await _dbContext.Rooms
            .AsNoTracking()
            .Where(r => r.Id == character.CurrentRoomId)
            .Select(r => new RoomSummary(
                r.Id,
                r.Name,
                r.Description,
                r.AccessType,
                r.MapX,
                r.MapY,
                r.MapLayer))
            .FirstOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            return null;
        }

        var exitRows = await _dbContext.RoomExits
            .AsNoTracking()
            .Where(e => e.SourceRoomId == character.CurrentRoomId && !e.IsHidden)
            .OrderBy(e => e.Direction)
            .Join(
                _dbContext.Rooms.AsNoTracking(),
                e => e.DestinationRoomId,
                r => r.Id,
                (e, r) => new
                {
                    e.Id,
                    e.Direction,
                    e.DestinationRoomId,
                    r.Name,
                    e.IsLocked,
                    r.AccessType,
                    r.EncounterInstanceId,
                })
            .ToListAsync(cancellationToken);

        // WF-25: a door into a job site's private room opens only for a
        // runner on that job, so for everyone else it is shown as what it
        // behaves as — locked.
        var exits = new List<RoomExitSummary>(exitRows.Count);
        foreach (var e in exitRows)
        {
            var locked = e.IsLocked
                || (e is { AccessType: RoomAccessType.Private, EncounterInstanceId: null }
                    && await _runAccess.FindEnterableRunAsync(character.Id, e.DestinationRoomId, cancellationToken) is null);
            exits.Add(new RoomExitSummary(e.Id, e.Direction, e.DestinationRoomId, e.Name, locked));
        }

        var occupants = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.CurrentRoomId == character.CurrentRoomId
                && c.LifecycleState == CharacterLifecycleState.Finalized)
            .OrderBy(c => c.Name)
            .Select(c => new CharacterSummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var npcs = await _dbContext.NpcInstances
            .AsNoTracking()
            .Where(n => n.RoomId == character.CurrentRoomId)
            .OrderBy(n => n.Name)
            .Select(n => new RoomNpcSummary(n.Id, n.Name))
            .ToListAsync(cancellationToken);

        // Viewer-relative rendering (§33): a hidden interactable appears only
        // when this character has a discovery row for it.
        var interactableSubjectType = nameof(DiscoverySubjectType.Interactable);
        var interactables = await _dbContext.RoomInteractables
            .AsNoTracking()
            .Where(i => i.RoomId == character.CurrentRoomId)
            .Where(i => !i.IsHidden || _dbContext.CharacterDiscoveries.Any(d =>
                d.CharacterId == character.Id
                && d.SubjectType == interactableSubjectType
                && d.SubjectId == i.Id))
            .OrderBy(i => i.Name)
            .Select(i => new RoomInteractableSummary(i.Id, i.Name, i.Description))
            .ToListAsync(cancellationToken);

        var (messages, olderCursor) = await LoadMessagesAsync(
            session.Id,
            session.StartAtUtc,
            session.EndedAtUtc,
            olderMessagesCursor,
            cancellationToken);

        return new RoomSession(
            session.Id,
            session.ExpiresAtUtc,
            new CharacterSummary(character.Id, character.Name),
            room,
            exits,
            occupants,
            npcs,
            interactables,
            messages,
            olderCursor,
            _combatTracker.Get(character.CurrentRoomId) is { } combat ? CombatView.From(combat) : null);
    }

    private async Task<(IReadOnlyList<RoomMessage> Messages, string? OlderCursor)> LoadMessagesAsync(
        Guid playSessionId,
        DateTimeOffset sessionStartAtUtc,
        DateTimeOffset? sessionEndedAtUtc,
        string? olderMessagesCursor,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAtUtc >= sessionStartAtUtc)
            .Where(m => sessionEndedAtUtc == null || m.CreatedAtUtc < sessionEndedAtUtc)
            .Where(m => _dbContext.RoomVisits.Any(v =>
                v.PlaySessionId == playSessionId &&
                v.RoomId == m.RoomId &&
                v.EnteredAtUtc <= m.CreatedAtUtc &&
                (v.LeftAtUtc == null || m.CreatedAtUtc < v.LeftAtUtc)));

        if (RoomSessionCursor.TryDecode(olderMessagesCursor, out var cursorTime, out var cursorId))
        {
            query = query.Where(m => m.CreatedAtUtc < cursorTime || (m.CreatedAtUtc == cursorTime && m.Id < cursorId));
        }

        var fetched = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Join(
                _dbContext.Characters.AsNoTracking(),
                m => m.CharacterId,
                c => c.Id,
                (m, c) => new RoomMessage(
                    m.Id,
                    m.RoomId,
                    m.CharacterId,
                    c.Name,
                    m.Content,
                    m.Type,
                    m.CreatedAtUtc))
            .Take(RoomSessionCursor.MessagePageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = fetched.Count > RoomSessionCursor.MessagePageSize;
        var page = fetched
            .Take(RoomSessionCursor.MessagePageSize)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToList();

        var olderCursor = hasMore
            ? RoomSessionCursor.Encode(page[0].CreatedAtUtc, page[0].Id)
            : null;

        return (page, olderCursor);
    }
}
