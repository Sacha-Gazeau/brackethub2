import type {
  BracketSlot,
  TournamentAdminTeam,
} from "../../../action/tournamentAdminStage";

type TournamentBracketManualEditorProps = {
  slots: BracketSlot[];
  teams: TournamentAdminTeam[];
  onChange: (nextSlots: BracketSlot[]) => void;
};

type SeededMatch = {
  matchKey: string;
  startSlot: number;
  endSlot: number;
  sourceLabel: string;
};

export default function TournamentBracketManualEditor({
  slots,
  teams,
  onChange,
}: TournamentBracketManualEditorProps) {
  const selectedIds = new Set(
    slots
      .map((slot) => slot.teamId)
      .filter((teamId): teamId is number => teamId !== null),
  );

  const rounds = buildRounds(slots.length);

  return (
    <div className="tournament-stage-editor">
      <div className="tournament-stage-editor__header">
        <h3>Bracket handmatig invullen</h3>
        <p>
          Plaats de teams rechtstreeks in ronde 1. De rest van de bracket wordt
          automatisch opgebouwd zodat je meteen ziet wie naar welke match doorgaat.
        </p>
      </div>

      <div className="tournament-bracket-seeding">
        {rounds.map((round, roundIndex) => (
          <section className="tournament-bracket-seeding__round" key={roundIndex}>
            <header className="tournament-bracket-seeding__round-header">
              <span>Ronde {roundIndex + 1}</span>
            </header>

            <div className="tournament-bracket-seeding__matches">
              {round.map((match) => (
                <article className="tournament-bracket-seeding__match" key={match.matchKey}>
                  <span className="tournament-bracket-seeding__match-label">
                    Match {extractMatchNumber(match.matchKey)}
                  </span>

                  {roundIndex === 0 ? (
                    <div className="tournament-bracket-seeding__slots">
                      {[match.startSlot, match.endSlot].map((slotIndex) => {
                        const slot = slots[slotIndex - 1];

                        return (
                          <label
                            className="tournament-bracket-seeding__slot"
                            key={slotIndex}
                          >
                            <span>Slot {slotIndex}</span>
                            <select
                              value={slot.teamId ?? ""}
                              onChange={(event) => {
                                const value = event.target.value;
                                onChange(
                                  slots.map((currentSlot) =>
                                    currentSlot.slotIndex === slot.slotIndex
                                      ? {
                                          ...currentSlot,
                                          teamId: value ? Number(value) : null,
                                        }
                                      : currentSlot,
                                  ),
                                );
                              }}
                            >
                              <option value="">Leeg</option>
                              {teams.map((team) => {
                                const isSelectedElsewhere =
                                  selectedIds.has(team.id) && slot.teamId !== team.id;

                                return (
                                  <option
                                    key={team.id}
                                    value={team.id}
                                    disabled={isSelectedElsewhere}
                                  >
                                    {team.name}
                                  </option>
                                );
                              })}
                            </select>
                          </label>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="tournament-bracket-seeding__winner-card">
                      <strong>Winnaar</strong>
                      <p>{match.sourceLabel}</p>
                    </div>
                  )}
                </article>
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

function buildRounds(slotCount: number): SeededMatch[][] {
  const rounds: SeededMatch[][] = [];
  let currentRound: SeededMatch[] = Array.from({ length: slotCount / 2 }, (_, index) => {
    const startSlot = index * 2 + 1;
    const endSlot = startSlot + 1;

    return {
      matchKey: `1-${index + 1}`,
      startSlot,
      endSlot,
      sourceLabel: `slot ${startSlot} vs slot ${endSlot}`,
    };
  });

  rounds.push(currentRound);

  let roundNumber = 2;
  while (currentRound.length > 1) {
    const nextRound: SeededMatch[] = [];

    for (let index = 0; index < currentRound.length; index += 2) {
      const left = currentRound[index];
      const right = currentRound[index + 1];

      nextRound.push({
        matchKey: `${roundNumber}-${nextRound.length + 1}`,
        startSlot: left.startSlot,
        endSlot: right.endSlot,
        sourceLabel: `van slots ${left.startSlot}-${left.endSlot} vs ${right.startSlot}-${right.endSlot}`,
      });
    }

    rounds.push(nextRound);
    currentRound = nextRound;
    roundNumber += 1;
  }

  return rounds;
}

function extractMatchNumber(matchKey: string): string {
  return matchKey.split("-")[1] ?? "1";
}
