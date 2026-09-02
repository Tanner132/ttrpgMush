namespace SeattleByNight.Domain.Enums;

public enum ChatMessageType
{
    Say = 0,
    Emote = 1,
    Roll = 2,
    // Milestone 7: room-visible text with no speaker — what an authored
    // trigger's narrate reaction broadcasts. Never persisted to chat history;
    // the audit log is where the record of what happened lives.
    Narration = 3
}
