using MediatR;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.RoomChat;

public sealed record SendRoomMessageCommand(Guid UserId, string Content, ChatMessageType Type) : IRequest<SendRoomMessageResult>;

public enum SendRoomMessageError
{
    None = 0,
    NoActiveSession,
    InvalidContent,
    InvalidType
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

    public SendRoomMessageCommandHandler(
        IRoomChatStore chatStore,
        PlaySessionOptions options)
    {
        _chatStore = chatStore;
        _options = options;
    }

    public async Task<SendRoomMessageResult> Handle(SendRoomMessageCommand request, CancellationToken cancellationToken)
    {
        if (request.Type is not (ChatMessageType.Say or ChatMessageType.Emote))
        {
            return SendRoomMessageResult.Failure(SendRoomMessageError.InvalidType);
        }

        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > MaxContentLength)
        {
            return SendRoomMessageResult.Failure(SendRoomMessageError.InvalidContent);
        }

        var outcome = await _chatStore.SendMessageAsync(
            request.UserId,
            request.Content,
            request.Type,
            _options.IdleTimeout,
            cancellationToken);

        if (outcome is null)
        {
            return SendRoomMessageResult.Failure(SendRoomMessageError.NoActiveSession);
        }

        return SendRoomMessageResult.Success(outcome.Message, outcome.ExpiresAtUtc);
    }
}
