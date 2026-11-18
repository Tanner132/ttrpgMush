using MediatR;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCareer;

// Workstream 2.1, owner decision `career.quality-removal-approval`. A player
// buying off a negative quality whose removal has a stated requirement makes a
// request (AdvanceQualityCommand); the Karma is held at once. A Storyteller
// approves it (the quality comes off the sheet) or denies it (the Karma is
// refunded), from the admin panel.
public sealed record QualityRemovalRequestRecord(
    Guid Id,
    Guid CharacterId,
    string CharacterName,
    Guid RequestedByUserId,
    string? RequestedByUserName,
    string QualityKey,
    string QualityId,
    int KarmaCost,
    string Requirement,
    string Explanation,
    QualityRemovalRequestStatus Status,
    Guid? ResolvedByUserId,
    string? ResolvedByUserName,
    string? ResolutionNote,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public enum ResolveQualityRemovalError
{
    None,
    NotFound,
    AlreadyResolved,
    CareerStateNotInitialized,
    Conflict,
}

public sealed record ResolveQualityRemovalResult(ResolveQualityRemovalError Error, QualityRemovalRequestRecord? Request = null)
{
    public bool Succeeded => Error == ResolveQualityRemovalError.None;
}

public interface IQualityRemovalRequestStore
{
    Task<IReadOnlySet<string>> GetPendingKeysAsync(Guid characterId, CancellationToken cancellationToken = default);

    // A character's own requests, newest first: every pending one plus the
    // most recently resolved, so the sheet can show a denial and its note.
    Task<IReadOnlyList<QualityRemovalRequestRecord>> GetForCharacterAsync(
        Guid characterId, int resolvedLimit, CancellationToken cancellationToken = default);

    // Pending requests oldest first (a queue); resolved ones newest first.
    Task<IReadOnlyList<QualityRemovalRequestRecord>> ListAsync(
        QualityRemovalRequestStatus status, int limit, CancellationToken cancellationToken = default);

    // Atomically resolves a pending request: approval removes the quality
    // instance and writes the advancement; denial refunds the held Karma.
    // Both bump the career-state version and append an audit record in the
    // same save.
    Task<ResolveQualityRemovalResult> ResolveAsync(
        Guid requestId, Guid storytellerUserId, bool approve, string? note, CancellationToken cancellationToken = default);
}

public sealed record ListQualityRemovalRequestsQuery(QualityRemovalRequestStatus Status)
    : IRequest<IReadOnlyList<QualityRemovalRequestRecord>>;

public sealed class ListQualityRemovalRequestsQueryHandler
    : IRequestHandler<ListQualityRemovalRequestsQuery, IReadOnlyList<QualityRemovalRequestRecord>>
{
    private const int Limit = 200;
    private readonly IQualityRemovalRequestStore store;

    public ListQualityRemovalRequestsQueryHandler(IQualityRemovalRequestStore store) => this.store = store;

    public Task<IReadOnlyList<QualityRemovalRequestRecord>> Handle(
        ListQualityRemovalRequestsQuery request, CancellationToken cancellationToken) =>
        store.ListAsync(request.Status, Limit, cancellationToken);
}

public sealed record ResolveQualityRemovalCommand(Guid StorytellerUserId, Guid RequestId, bool Approve, string? Note)
    : IRequest<ResolveQualityRemovalResult>;

public sealed class ResolveQualityRemovalCommandHandler
    : IRequestHandler<ResolveQualityRemovalCommand, ResolveQualityRemovalResult>
{
    public const int MaxNoteLength = 2000;

    private readonly IQualityRemovalRequestStore store;

    public ResolveQualityRemovalCommandHandler(IQualityRemovalRequestStore store) => this.store = store;

    public Task<ResolveQualityRemovalResult> Handle(ResolveQualityRemovalCommand request, CancellationToken cancellationToken)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            note = note[..MaxNoteLength];
        }

        return store.ResolveAsync(request.RequestId, request.StorytellerUserId, request.Approve, note, cancellationToken);
    }
}
