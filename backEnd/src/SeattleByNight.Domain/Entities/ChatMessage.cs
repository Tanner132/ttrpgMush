using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid CharacterId { get; set; }
    public ChatMessageType Type { get; set; } = ChatMessageType.Say;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
