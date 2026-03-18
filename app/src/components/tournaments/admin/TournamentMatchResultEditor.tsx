import { useState } from "react";
import type {
  ExistingMatch,
  TournamentAdminTeam,
} from "../../../action/tournamentAdminStage";
import { getWinsNeededForMatch } from "../../../action/tournamentAdminStage";

type TournamentMatchResultEditorProps = {
  matches: ExistingMatch[];
  teams: TournamentAdminTeam[];
  formatName: string;
  finalFormatName: string | null;
  savingMatchId: number | null;
  onSave: (matchId: number, team1Score: number, team2Score: number) => void;
};

type ScoreState = Record<number, { team1Score: string; team2Score: string }>;

function getInitialScoreValue(score: number, hasResult: boolean): string {
  if (!hasResult && score === 0) {
    return "";
  }

  return String(score);
}

function buildMatchScoreState(
  team1Score: number,
  team2Score: number,
  winnerId: number | null,
): { team1Score: string; team2Score: string } {
  const hasResult = winnerId !== null || team1Score > 0 || team2Score > 0;

  return {
    team1Score: getInitialScoreValue(team1Score, hasResult),
    team2Score: getInitialScoreValue(team2Score, hasResult),
  };
}

export default function TournamentMatchResultEditor({
  matches,
  teams,
  formatName,
  finalFormatName,
  savingMatchId,
  onSave,
}: TournamentMatchResultEditorProps) {
  const [scoresByMatchId, setScoresByMatchId] = useState<ScoreState>({});
  const [activeRound, setActiveRound] = useState<number | null>(null);
  const teamNameById = new Map(teams.map((team) => [team.id, team.name]));
  const maxRound = Math.max(...matches.map((match) => match.roundNumber), 1);
  const rounds = Array.from(new Set(matches.map((match) => match.roundNumber))).sort(
    (left, right) => left - right,
  );
  const resolvedActiveRound =
    activeRound !== null && rounds.includes(activeRound) ? activeRound : (rounds[0] ?? null);
  const visibleMatches = matches.filter((match) => match.roundNumber === resolvedActiveRound);

  return (
    <section className="tournament-stage-results">
      <div className="tournament-stage-results__header">
        <h2>Matchresultaten</h2>
        <p>Werk de scores match per match bij. De winnaar gaat automatisch door.</p>
      </div>

      <div className="tournament-stage-tabs">
        {rounds.map((round) => (
          <button
            key={round}
            type="button"
            className={resolvedActiveRound === round ? "active" : ""}
            onClick={() => setActiveRound(round)}
          >
            {getRoundLabel(round, maxRound)}
          </button>
        ))}
      </div>

      <div className="tournament-stage-results__grid">
        {visibleMatches.map((match) => {
          const winsNeeded = getWinsNeededForMatch(
            match.roundNumber,
            maxRound,
            formatName,
            finalFormatName,
          );

          const currentScores =
            scoresByMatchId[match.id] ??
            buildMatchScoreState(match.team1Score, match.team2Score, match.winnerId);

          return (
            <article className="tournament-stage-results__card" key={match.id}>
              <div className="tournament-stage-results__meta">
                <span>Ronde {match.roundNumber}</span>
                <span>Match {match.matchNumber}</span>
                <span>Tot {winsNeeded}</span>
              </div>

              <div className="tournament-stage-results__team">
                <strong
                  className={
                    match.winnerId === match.team1Id
                      ? "tournament-stage-results__winner"
                      : ""
                  }
                >
                  {teamNameById.get(match.team1Id ?? -1) ?? "Nog te bepalen"}
                </strong>
                <input
                  type="number"
                  min={0}
                  max={winsNeeded}
                  placeholder="0"
                  value={currentScores.team1Score}
                  onChange={(event) =>
                    setScoresByMatchId((current) => ({
                      ...current,
                      [match.id]: {
                        ...currentScores,
                        team1Score: event.target.value,
                      },
                    }))
                  }
                />
              </div>

              <div className="tournament-stage-results__team">
                <strong
                  className={
                    match.winnerId === match.team2Id
                      ? "tournament-stage-results__winner"
                      : ""
                  }
                >
                  {teamNameById.get(match.team2Id ?? -1) ?? "Nog te bepalen"}
                </strong>
                <input
                  type="number"
                  min={0}
                  max={winsNeeded}
                  placeholder="0"
                  value={currentScores.team2Score}
                  onChange={(event) =>
                    setScoresByMatchId((current) => ({
                      ...current,
                      [match.id]: {
                        ...currentScores,
                        team2Score: event.target.value,
                      },
                    }))
                  }
                />
              </div>

              <div className="tournament-stage-results__actions">
                <button
                  type="button"
                  className="btn btn--primary btn--sm"
                  disabled={savingMatchId === match.id}
                  onClick={() =>
                    onSave(
                      match.id,
                      Number(currentScores.team1Score || 0),
                      Number(currentScores.team2Score || 0),
                    )
                  }
                >
                  {savingMatchId === match.id ? "Opslaan..." : "Resultaat opslaan"}
                </button>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}

function getRoundLabel(round: number, maxRound: number): string {
  if (round === maxRound) {
    return "Finale";
  }

  if (round === maxRound - 1) {
    return "Halve finale";
  }

  if (round === maxRound - 2) {
    return "Kwartfinale";
  }

  return `Ronde ${round}`;
}
