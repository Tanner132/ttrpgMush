namespace SeattleByNight.Domain.Enums;

// Milestone 7: the draft → publish → retire lifecycle of one authored
// definition. Only Published definitions are composed into the document the
// game serves; drafts are freely editable and invisible to play.
public enum GameContentStatus
{
    // Never been live: the game does not see it, it is freely editable,
    // and it is the only status a hard delete is ever allowed to touch.
    Draft,
    // Live: the published payload is what encounters and missions are built
    // from. A published definition may still carry newer draft edits, which
    // only reach play at the next publish.
    Published,
    // Was live, taken out of play. The payload stays exactly as it was and the
    // definition stays in the served document — instances already running have
    // to keep resolving what they were built from — but nothing new is offered
    // it. Publishing it again is what reverses this.
    Retired,
}
