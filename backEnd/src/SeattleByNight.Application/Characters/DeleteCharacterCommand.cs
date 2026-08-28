using MediatR;

namespace SeattleByNight.Application.Characters;

public sealed record DeleteCharacterCommand(Guid UserId, Guid CharacterId) : IRequest<DeleteCharacterResult>;

public enum DeleteCharacterError
{
    None,
    NotFound,
}

public sealed record DeleteCharacterResult(DeleteCharacterError Error)
{
    public bool Succeeded => Error == DeleteCharacterError.None;

    public static DeleteCharacterResult Success() => new(DeleteCharacterError.None);

    public static DeleteCharacterResult Failure(DeleteCharacterError error) => new(error);
}

public sealed class DeleteCharacterCommandHandler : IRequestHandler<DeleteCharacterCommand, DeleteCharacterResult>
{
    private readonly ICharacterStore _store;

    public DeleteCharacterCommandHandler(ICharacterStore store)
    {
        _store = store;
    }

    public async Task<DeleteCharacterResult> Handle(DeleteCharacterCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _store.DeleteAsync(request.UserId, request.CharacterId, cancellationToken);
        return deleted ? DeleteCharacterResult.Success() : DeleteCharacterResult.Failure(DeleteCharacterError.NotFound);
    }
}
