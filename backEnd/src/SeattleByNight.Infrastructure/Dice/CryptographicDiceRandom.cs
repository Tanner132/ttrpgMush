using System.Security.Cryptography;
using SeattleByNight.Application.Dice;

namespace SeattleByNight.Infrastructure.Dice;

public sealed class CryptographicDiceRandom : IDiceRandom
{
    public int GetInt32(int fromInclusive, int toExclusive) =>
        RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);
}
