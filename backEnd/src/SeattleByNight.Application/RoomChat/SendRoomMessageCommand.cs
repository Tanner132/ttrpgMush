using MediatR;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Application.RoomChat;

public sealed record SendRoomMessageCommand(Guid UserId, string Content) : IRequest<SendRoomMessageResult>;

public enum SendRoomMessageError
{
    None = 0,
    NoActiveSession,
    InvalidContent
}

public sealed record SendRoomMessageResult(SendRoomMessageError Error, RoomMessage? Message, DateTimeOffset? ExpiresAtUtc)
{
    public bool IsSuccess => Error == SendRoomMessageError.None;

    public static SendRoomMessageResult Success(RoomMessage message, DateTimeOffset expiresAtUtc) =>
        new(SendRoomMessageError.None, message, expiresAtUtc);

    public static SendRoomMessageResult Failure(SendRoomMessageError error) => new(error, null, null);
}

public sealed class SendRoomMessageCommandHandler : IRequestHandler<SendRoomMessageCommand, SendRoomMessageResult>
{
    public const int MaxContentLength = 4000;

    private readonly IRoomChatStore _chatStore;
    private readonly PlaySessionOptions _options;
    private readonly TimeProvider _timeProvider;

    public SendRoomMessageCommandHandler(
        IRoomChatStore chatStore,
        PlaySessionOptions options,
        TimeProvider timeProvider)
    {
        _chatStore = chatStore;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<SendRoomMessageResult> Handle(SendRoomMessageCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > MaxContentLength)
        {
            return SendRoomMessageResult.Failure(SendRoomMessageError.InvalidContent);
        }

        var outcome = await _chatStore.SendMessageAsync(
            request.UserId,
            request.Content,
            _timeProvider.GetUtcNow(),
            _options.IdleTimeout,
            cancellationToken);

        if (outcome is null)
        {
            return SendRoomMessageResult.Failure(SendRoomMessageError.NoActiveSession);
        }

        return SendRoomMessageResult.Success(outcome.Message, outcome.ExpiresAtUtc);
    }
}
