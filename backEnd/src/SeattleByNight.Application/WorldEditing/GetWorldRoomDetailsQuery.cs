using MediatR;

namespace SeattleByNight.Application.WorldEditing;

public sealed record GetWorldRoomDetailsQuery(Guid RoomId) : IRequest<WorldRoomDetails?>;

public sealed class GetWorldRoomDetailsQueryHandler : IRequestHandler<GetWorldRoomDetailsQuery, WorldRoomDetails?>
{
    private readonly IWorldGraphReader _reader;

    public GetWorldRoomDetailsQueryHandler(IWorldGraphReader reader)
    {
        _reader = reader;
    }

    public Task<WorldRoomDetails?> Handle(
        GetWorldRoomDetailsQuery request,
        CancellationToken cancellationToken)
        => _reader.GetRoomDetailsAsync(request.RoomId, cancellationToken);
}
