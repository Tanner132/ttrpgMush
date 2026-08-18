using MediatR;

namespace SeattleByNight.Application.WorldEditing;

public sealed record CreateRoomExitCommand(Guid ActorUserId, RoomExitMutation Mutation)
    : IRequest<WorldMutationResult<WorldExit>>;

public sealed record UpdateRoomExitCommand(Guid ActorUserId, Guid ExitId, Guid Version, RoomExitMutation Mutation)
    : IRequest<WorldMutationResult<WorldExit>>;

public sealed class CreateRoomExitCommandHandler : IRequestHandler<CreateRoomExitCommand, WorldMutationResult<WorldExit>>
{
    private readonly IWorldEditorStore _store;

    public CreateRoomExitCommandHandler(IWorldEditorStore store) => _store = store;

    public Task<WorldMutationResult<WorldExit>> Handle(CreateRoomExitCommand request, CancellationToken cancellationToken)
    {
        var errors = WorldMutationValidation.ValidateExit(request.Mutation, false, Guid.Empty);
        return errors.Count == 0
            ? _store.CreateExitAsync(request.ActorUserId, request.Mutation, cancellationToken)
            : Task.FromResult(WorldMutationResult<WorldExit>.Invalid(errors));
    }
}

public sealed class UpdateRoomExitCommandHandler : IRequestHandler<UpdateRoomExitCommand, WorldMutationResult<WorldExit>>
{
    private readonly IWorldEditorStore _store;

    public UpdateRoomExitCommandHandler(IWorldEditorStore store) => _store = store;

    public Task<WorldMutationResult<WorldExit>> Handle(UpdateRoomExitCommand request, CancellationToken cancellationToken)
    {
        var errors = WorldMutationValidation.ValidateExit(request.Mutation, true, request.Version);
        return errors.Count == 0
            ? _store.UpdateExitAsync(request.ActorUserId, request.ExitId, request.Version, request.Mutation, cancellationToken)
            : Task.FromResult(WorldMutationResult<WorldExit>.Invalid(errors));
    }
}
