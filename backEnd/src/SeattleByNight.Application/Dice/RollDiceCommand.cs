using MediatR;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Dice;

public sealed record RollDiceCommand(Guid UserId, string Expression) : IRequest<RollDiceResult>;

public enum RollDiceError
{
    None = 0,
    NoActiveSession,
    InvalidExpression
}

public sealed record RollDiceResult(
    RollDiceError Error,
    string? ErrorMessage,
    RoomMessage? Message,
    DateTimeOffset? ExpiresAtUtc)
{
    public bool IsSuccess => Error == RollDiceError.None;

    public static RollDiceResult Success(RoomMessage message, DateTimeOffset expiresAtUtc) =>
        new(RollDiceError.None, null, message, expiresAtUtc);

    public static RollDiceResult InvalidExpression(string message) =>
        new(RollDiceError.InvalidExpression, message, null, null);

    public static RollDiceResult Failure(RollDiceError error) => new(error, null, null, null);
}

public sealed class RollDiceCommandHandler : IRequestHandler<RollDiceCommand, RollDiceResult>
{
    private readonly IRoomChatStore _chatStore;
    private readonly IDiceEngine _diceEngine;
    private readonly DiceOptions _diceOptions;
    private readonly PlaySessionOptions _playSessionOptions;

    public RollDiceCommandHandler(
        IRoomChatStore chatStore,
        IDiceEngine diceEngine,
        DiceOptions diceOptions,
        PlaySessionOptions playSessionOptions)
    {
        _chatStore = chatStore;
        _diceEngine = diceEngine;
        _diceOptions = diceOptions;
        _playSessionOptions = playSessionOptions;
    }

    public async Task<RollDiceResult> Handle(RollDiceCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Expression) ||
            request.Expression.Length > _diceOptions.MaxExpressionLength)
        {
            return RollDiceResult.InvalidExpression(
                $"Dice expression must be between 1 and {_diceOptions.MaxExpressionLength} characters.");
        }

        if (!_diceEngine.TryParse(request.Expression, out var expression, out var error))
        {
            return RollDiceResult.InvalidExpression(error!);
        }

        var rolls = _diceEngine.Roll(expression!);
        var total = rolls.Sum() + expression!.Modifier;
        var content = DiceResultFormatter.Format(expression, rolls, total);

        var outcome = await _chatStore.SendMessageAsync(
            request.UserId,
            content,
            ChatMessageType.Roll,
            _playSessionOptions.IdleTimeout,
            cancellationToken);

        if (outcome is null)
        {
            return RollDiceResult.Failure(RollDiceError.NoActiveSession);
        }

        return RollDiceResult.Success(outcome.Message, outcome.ExpiresAtUtc);
    }
}
