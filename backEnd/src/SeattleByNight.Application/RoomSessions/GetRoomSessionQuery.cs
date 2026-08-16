using MediatR;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Application.RoomSessions;

public sealed record GetRoomSessionQuery(Guid UserId, string? OlderMessagesCursor) : IRequest<GetRoomSessionResult>;

public enum GetRoomSessionError
{
    None = 0,
    NoActiveSession,
    NotFound
}

public sealed record GetRoomSessionResult(GetRoomSessionError Error, RoomSession? Session)
{
    public bool IsSuccess => Error == GetRoomSessionError.None;

    public static GetRoomSessionResult Success(RoomSession session) => new(GetRoomSessionError.None, session);

    public static GetRoomSessionResult Failure(GetRoomSessionError error) => new(error, null);
}

public sealed class GetRoomSessionQueryHandler : IRequestHandler<GetRoomSessionQuery, GetRoomSessionResult>
{
    private readonly IPlaySessionStore _playSessionStore;
    private readonly IRoomSessionReader _reader;
    private readonly TimeProvider _timeProvider;

    public GetRoomSessionQueryHandler(IPlaySessionStore playSessionStore, IRoomSessionReader reader, TimeProvider timeProvider)
    {
        _playSessionStore = playSessionStore;
        _reader = reader;
        _timeProvider = timeProvider;
    }

    public async Task<GetRoomSessionResult> Handle(GetRoomSessionQuery request, CancellationToken cancellationToken)
    {
        var active = await _playSessionStore.GetActiveByUserIdAsync(request.UserId, _timeProvider.GetUtcNow(), cancellationToken);

        if (active is null)
        {
            return GetRoomSessionResult.Failure(GetRoomSessionError.NoActiveSession);
        }

        var session = await _reader.GetByPlaySessionIdAsync(active.Id, request.OlderMessagesCursor, cancellationToken);

        if (session is null)
        {
            return GetRoomSessionResult.Failure(GetRoomSessionError.NotFound);
        }

        return GetRoomSessionResult.Success(session);
    }
}
