import type { TournamentAdminStageSummary } from "../../../action/tournamentAdminStage";

type TournamentStageSummaryProps = {
  tournament: TournamentAdminStageSummary;
  acceptedTeamsCount: number;
  canLaunch: boolean;
};

export default function TournamentStageSummary({
  tournament,
  acceptedTeamsCount,
  canLaunch,
}: TournamentStageSummaryProps) {
  const statusMessage =
    tournament.tournament_type !== "pending"
      ? "De toernooistructuur is al gestart."
      : canLaunch
        ? "Het toernooi kan gestart worden."
        : "Het toernooi kan nog niet gestart worden.";

  return (
    <section className="tournament-stage-summary">
      <div className="tournament-stage-summary__card">
        <span>Geaccepteerde teams</span>
        <strong>{acceptedTeamsCount}</strong>
      </div>
      <div className="tournament-stage-summary__card">
        <span>Minimum vereist</span>
        <strong>{tournament.min_teams}</strong>
      </div>
      <div className="tournament-stage-summary__card">
        <span>Maximum</span>
        <strong>{tournament.max_teams}</strong>
      </div>
      <div className="tournament-stage-summary__card">
        <span>Huidige status</span>
        <strong>{tournament.tournament_type}</strong>
      </div>
      <div className="tournament-stage-summary__status">
        <p>{statusMessage}</p>
      </div>
    </section>
  );
}
