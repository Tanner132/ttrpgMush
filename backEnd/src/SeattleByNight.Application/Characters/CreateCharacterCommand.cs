using MediatR;

namespace SeattleByNight.Application.Characters;

public sealed record CreateCharacterCommand(Guid UserId, string Name) : IRequest<CreateCharacterResult>;

public sealed class CreateCharacterCommandHandler : IRequestHandler<CreateCharacterCommand, CreateCharacterResult>
{
    private readonly ICharacterStore _store;
    private readonly WorldOptions _options;

    public CreateCharacterCommandHandler(ICharacterStore store, WorldOptions options)
    {
        _store = store;
        _options = options;
    }

    public async Task<CreateCharacterResult> Handle(CreateCharacterCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length is < 2 or > 50)
        {
            return CreateCharacterResult.Failure(CreateCharacterError.InvalidName);
        }

        var normalizedName = name.ToUpperInvariant();

        return await _store.CreateAsync(request.UserId, name, normalizedName, _options.StartingRoomId, cancellationToken);
    }
}
