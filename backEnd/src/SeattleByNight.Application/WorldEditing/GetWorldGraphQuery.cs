using MediatR;

namespace SeattleByNight.Application.WorldEditing;

public sealed record GetWorldGraphQuery : IRequest<WorldGraph?>;

public sealed class GetWorldGraphQueryHandler : IRequestHandler<GetWorldGraphQuery, WorldGraph?>
{
    private readonly IWorldGraphReader _reader;

    public GetWorldGraphQueryHandler(IWorldGraphReader reader)
    {
        _reader = reader;
    }

    public Task<WorldGraph?> Handle(GetWorldGraphQuery request, CancellationToken cancellationToken)
        => _reader.GetGraphAsync(cancellationToken);
}
