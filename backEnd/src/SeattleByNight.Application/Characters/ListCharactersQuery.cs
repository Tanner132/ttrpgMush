using MediatR;

namespace SeattleByNight.Application.Characters;

public sealed record ListCharactersQuery(Guid UserId) : IRequest<IReadOnlyList<CharacterSummary>>;

public sealed class ListCharactersQueryHandler : IRequestHandler<ListCharactersQuery, IReadOnlyList<CharacterSummary>>
{
    private readonly ICharacterStore _store;

    public ListCharactersQueryHandler(ICharacterStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<CharacterSummary>> Handle(ListCharactersQuery request, CancellationToken cancellationToken)
        => _store.ListByUserIdAsync(request.UserId, cancellationToken);
}
