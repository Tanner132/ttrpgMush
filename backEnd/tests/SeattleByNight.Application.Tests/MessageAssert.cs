namespace SeattleByNight.Application.Tests;

// Assert.Contains truncates both strings on failure; an author-facing error
// message is the thing under test, so a failure shows it whole.
internal static class MessageAssert
{
    public static void Has(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new Xunit.Sdk.XunitException($"Expected the message to contain:\n  {expected}\nbut it was:\n  {actual}");
        }
    }
}
