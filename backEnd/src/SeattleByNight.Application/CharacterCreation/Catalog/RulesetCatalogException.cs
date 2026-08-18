namespace SeattleByNight.Application.CharacterCreation.Catalog;

public sealed class RulesetCatalogException : Exception
{
    public RulesetCatalogException(string message)
        : base(message)
    {
    }

    public RulesetCatalogException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
