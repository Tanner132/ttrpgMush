using System.Security.Cryptography;
using SeattleByNight.Application.GameEngine.Dice;

namespace SeattleByNight.Infrastructure.GameEngine;

// Live rolls seed from the CSPRNG so players cannot predict results; the
// seed is recorded on every resolution, which is what makes the otherwise
// unpredictable roll replayable (§19).
public sealed class CryptographicSeedSource : ISeedSource
{
    public long NextSeed()
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        RandomNumberGenerator.Fill(buffer);
        return BitConverter.ToInt64(buffer);
    }
}
