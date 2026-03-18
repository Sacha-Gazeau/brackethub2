import { useState } from "react";
import type {
  ExistingGroup,
  ExistingMatch,
  TournamentAdminTeam,
  TournamentStageType,
} from "../../../action/tournamentAdminStage";

type TournamentStagePreviewProps = {
  type: TournamentStageType;
  groups: ExistingGroup[];
  matches: ExistingMatch[];
  teams: TournamentAdminTeam[];
};

export default function TournamentStagePreview({
  type,
  groups,
  matches,
  teams,
}: TournamentStagePreviewProps) {
  const [activeTab, setActiveTab] = useState<string>("groups");
  const teamMap = new Map(teams.map((team) => [team.id, team.name]));
  const rounds = Array.from(new Set(matches.map((match) => match.roundNumber))).sort(
    (left, right) => left - right,
  );
  const defaultTab =
    type === "groups_to_bracket" && groups.length > 0
      ? "groups"
      : rounds.length > 0
        ? `round-${rounds[0]}`
        : "groups";
  const resolvedActiveTab = getResolvedActiveTab(activeTab, defaultTab, rounds, type, groups.length);

  return (
    <details className="tournament-stage-preview">
      <summary className="tournament-stage-preview__summary">
        <span>Bestaande structuur</span>
        <span>Details tonen</span>
      </summary>

      <div className="tournament-stage-tabs">
        {type === "groups_to_bracket" && groups.length > 0 && (
          <button
            type="button"
            className={resolvedActiveTab === "groups" ? "active" : ""}
            onClick={() => setActiveTab("groups")}
          >
            Poules
          </button>
        )}
        {rounds.map((round) => (
          <button
            key={round}
            type="button"
            className={resolvedActiveTab === `round-${round}` ? "active" : ""}
            onClick={() => setActiveTab(`round-${round}`)}
          >
            {getRoundLabel(round, rounds.length)}
          </button>
        ))}
      </div>

      {type === "groups_to_bracket" && groups.length > 0 && resolvedActiveTab === "groups" && (
        <div className="tournament-stage-groups">
          {groups.map((group) => (
            <div className="tournament-stage-groups__card" key={group.id}>
              <h3>Groep {group.name}</h3>
              <ul>
                {group.teams.map((team) => (
                  <li key={team.id}>{team.name}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}

      {matches.length > 0 && resolvedActiveTab.startsWith("round-") && (
        <div className="tournament-stage-preview__matches">
          {matches
            .filter((match) => resolvedActiveTab === `round-${match.roundNumber}`)
            .map((match) => (
              <div className="tournament-stage-preview__match" key={match.id}>
                <span>
                  Ronde {match.roundNumber} - Match {match.matchNumber}
                </span>
                <strong>
                  {teamMap.get(match.team1Id ?? -1) ?? "Nog te bepalen"} vs{" "}
                  {teamMap.get(match.team2Id ?? -1) ?? "Nog te bepalen"}
                </strong>
              </div>
            ))}
        </div>
      )}
    </details>
  );
}

function getResolvedActiveTab(
  activeTab: string,
  defaultTab: string,
  rounds: number[],
  type: TournamentStageType,
  groupsLength: number,
): string {
  if (activeTab === "groups") {
    return type === "groups_to_bracket" && groupsLength > 0 ? "groups" : defaultTab;
  }

  if (activeTab.startsWith("round-")) {
    const round = Number(activeTab.replace("round-", ""));
    return rounds.includes(round) ? activeTab : defaultTab;
  }

  return defaultTab;
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
