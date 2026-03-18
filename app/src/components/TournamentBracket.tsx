import { useState } from "react";
import { useTranslation } from "react-i18next";

type TournamentBracketMatch = {
  id: number;
  round: number;
  match_number: number;
  team1_id: number | null;
  team2_id: number | null;
  winner_id: number | null;
  team1_score: number;
  team2_score: number;
};

type TournamentBracketProps = {
  matches: TournamentBracketMatch[];
  teamNameById: Map<number, string>;
  emptyLabel: string;
  placeholderLabel: string;
};

export default function TournamentBracket({
  matches,
  teamNameById,
  emptyLabel,
  placeholderLabel,
}: TournamentBracketProps) {
  const { t } = useTranslation();
  const [activeRound, setActiveRound] = useState<number | null>(null);
  const [activePageByRound, setActivePageByRound] = useState<
    Record<number, number>
  >({});
  const matchesPerPage = 4;
  const rounds = Array.from(
    matches.reduce((map, match) => {
      const currentRound = map.get(match.round) ?? [];
      currentRound.push(match);
      map.set(match.round, currentRound);
      return map;
    }, new Map<number, TournamentBracketMatch[]>()),
  ).sort(([left], [right]) => left - right);
  const resolvedActiveRound =
    activeRound !== null && rounds.some(([round]) => round === activeRound)
      ? activeRound
      : (rounds[0]?.[0] ?? null);

  if (matches.length === 0) {
    return (
      <div className="bracket-empty-state">
        <p>{emptyLabel}</p>
      </div>
    );
  }

  const visibleRound = rounds.find(([round]) => round === resolvedActiveRound) ?? rounds[0];
  if (!visibleRound) {
    return null;
  }

  const currentPage = Math.min(
    activePageByRound[visibleRound[0]] ?? 1,
    Math.max(1, Math.ceil(visibleRound[1].length / matchesPerPage)),
  );
  const pageCount = Math.ceil(visibleRound[1].length / matchesPerPage);
  const pageStart = (currentPage - 1) * matchesPerPage;
  const pageEnd = pageStart + matchesPerPage;
  const visibleMatches = visibleRound[1].slice(pageStart, pageEnd);

  return (
    <div className="bracket-view">
      <div className="tournament-tabs tournament-tabs--flush">
        {rounds.map(([round]) => (
          <button
            key={round}
            type="button"
            className={resolvedActiveRound === round ? "active" : ""}
            onClick={() => setActiveRound(round)}
          >
            {getRoundLabel(round, rounds.length)}
          </button>
        ))}
      </div>

      <div className="bracket-board bracket-board--single-round">
        <div className="bracket-round">
          <div className="bracket-round__header">
            <div className="bracket-round__title">
              {getRoundLabel(visibleRound[0], rounds.length)}
            </div>
            {pageCount > 1 && (
              <div className="bracket-round__range">
                {t("tournamentDetail.pagination.matches", {
                  start: pageStart + 1,
                  end: Math.min(pageEnd, visibleRound[1].length),
                })}
              </div>
            )}
          </div>

          {pageCount > 1 && (
            <div className="bracket-pagination" aria-label={t("tournamentDetail.pagination.label")}>
              {Array.from({ length: pageCount }, (_, index) => {
                const page = index + 1;

                return (
                  <button
                    key={page}
                    type="button"
                    className={page === currentPage ? "active" : ""}
                    onClick={() =>
                      setActivePageByRound((previous) => ({
                        ...previous,
                        [visibleRound[0]]: page,
                      }))
                    }
                  >
                    {t("tournamentDetail.pagination.page", { page })}
                  </button>
                );
              })}
            </div>
          )}

          <div className="bracket-round__matches">
            {visibleMatches.map((match) => (
              <div className="bracket-match-card" key={match.id}>
                <span className="bracket-match-card__number">
                  Match {match.match_number}
                </span>
                <div className="bracket-match-card__teams">
                  <div
                    className={`bracket-team-row ${
                      match.winner_id !== null && match.winner_id === match.team1_id
                        ? "bracket-team-row--winner"
                        : ""
                    }`}
                  >
                    <strong>{teamNameById.get(match.team1_id ?? -1) ?? placeholderLabel}</strong>
                    <span>{match.team1_score}</span>
                  </div>
                  <div
                    className={`bracket-team-row ${
                      match.winner_id !== null && match.winner_id === match.team2_id
                        ? "bracket-team-row--winner"
                        : ""
                    }`}
                  >
                    <strong>{teamNameById.get(match.team2_id ?? -1) ?? placeholderLabel}</strong>
                    <span>{match.team2_score}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function getRoundLabel(round: number, totalRounds: number): string {
  if (round === totalRounds) {
    return "Finale";
  }

  if (round === totalRounds - 1) {
    return "Halve finale";
  }

  if (round === totalRounds - 2) {
    return "Kwartfinale";
  }

  return `Ronde ${round}`;
}
