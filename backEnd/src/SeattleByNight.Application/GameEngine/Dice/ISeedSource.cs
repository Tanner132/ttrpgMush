namespace SeattleByNight.Application.GameEngine.Dice;

// Produces the unpredictable seed each live resolution rolls from; the seed
// is then recorded in the resolution result and audit record so the roll can
// be replayed deterministically (§19). Tests supply fixed seeds directly.
public interface ISeedSource
{
    long NextSeed();
}
