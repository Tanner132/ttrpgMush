namespace SeattleByNight.Application.CharacterCreation.Evaluation;

// Pure Inherent Limit, Condition Monitor, and Initiative formulas (sr5-core
// p. 101, PDF 103), extracted out of DerivedStatisticsEvaluator so the career
// composer (CareerSheetComposer) can recompute the same values against
// post-advancement attributes without reusing that evaluator's creation-only
// orchestration (SHEET-901 §2).
public static class DerivedStatisticsFormulas
{
    public const int InitiativeDiceBase = 1;
    private const int ConditionMonitorBase = 8;

    public static int PhysicalLimit(int strength, int body, int reaction) =>
        CeilDiv3(strength * 2 + body + reaction);

    public static int MentalLimit(int logic, int intuition, int willpower) =>
        CeilDiv3(logic * 2 + intuition + willpower);

    public static int SocialLimit(int charisma, int willpower, decimal essence) =>
        CeilDiv3(charisma * 2 + willpower + essence);

    public static int PhysicalConditionMonitor(int body) => CeilDiv2(body) + ConditionMonitorBase;

    public static int StunConditionMonitor(int willpower) => CeilDiv2(willpower) + ConditionMonitorBase;

    public static int InitiativeBase(int reaction, int intuition) => reaction + intuition;

    private static int CeilDiv3(decimal numerator) => (int)Math.Ceiling(numerator / 3m);

    private static int CeilDiv2(int numerator) => (int)Math.Ceiling(numerator / 2m);
}
