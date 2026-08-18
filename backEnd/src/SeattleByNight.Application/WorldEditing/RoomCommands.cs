using MediatR;

namespace SeattleByNight.Application.WorldEditing;

public sealed record CreateRoomCommand(Guid ActorUserId, CreateRoomMutation Mutation)
    : IRequest<WorldMutationResult<WorldRoom>>;

public sealed record UpdateRoomCommand(Guid ActorUserId, Guid RoomId, Guid Version, UpdateRoomMutation Mutation)
    : IRequest<WorldMutationResult<WorldRoom>>;

public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, WorldMutationResult<WorldRoom>>
{
    private readonly IWorldEditorStore _store;

    public CreateRoomCommandHandler(IWorldEditorStore store) => _store = store;

    public Task<WorldMutationResult<WorldRoom>> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var errors = WorldMutationValidation.ValidateCreateRoom(request.Mutation);
        return errors.Count == 0
            ? _store.CreateRoomAsync(request.ActorUserId, request.Mutation, cancellationToken)
            : Task.FromResult(WorldMutationResult<WorldRoom>.Invalid(errors));
    }
}

public sealed class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, WorldMutationResult<WorldRoom>>
{
    private readonly IWorldEditorStore _store;

    public UpdateRoomCommandHandler(IWorldEditorStore store) => _store = store;

    public Task<WorldMutationResult<WorldRoom>> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var errors = WorldMutationValidation.ValidateUpdateRoom(request.Mutation, request.Version);
        return errors.Count == 0
            ? _store.UpdateRoomAsync(request.ActorUserId, request.RoomId, request.Version, request.Mutation, cancellationToken)
            : Task.FromResult(WorldMutationResult<WorldRoom>.Invalid(errors));
    }
}
