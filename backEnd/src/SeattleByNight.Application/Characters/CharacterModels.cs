namespace SeattleByNight.Application.Characters;

public sealed record CharacterSummary(Guid Id, string Name);

public enum CreateCharacterError
{
    None = 0,
    InvalidName,
    LimitReached,
    NameTaken
}

public sealed record CreateCharacterResult(CreateCharacterError Error, CharacterSummary? Character)
{
    public bool IsSuccess => Error == CreateCharacterError.None;

    public static CreateCharacterResult Success(CharacterSummary character) => new(CreateCharacterError.None, character);

    public static CreateCharacterResult Failure(CreateCharacterError error) => new(error, null);
}
