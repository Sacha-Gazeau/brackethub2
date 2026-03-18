import type {
  GroupDefinition,
  TournamentAdminTeam,
} from "../../../action/tournamentAdminStage";

type TournamentGroupsManualEditorProps = {
  teams: TournamentAdminTeam[];
  groups: GroupDefinition[];
  onChange: (nextGroups: GroupDefinition[]) => void;
};

export default function TournamentGroupsManualEditor({
  teams,
  groups,
  onChange,
}: TournamentGroupsManualEditorProps) {
  const groupByTeamId = new Map<number, string>();

  groups.forEach((group) => {
    group.teamIds.forEach((teamId) => {
      groupByTeamId.set(teamId, group.key);
    });
  });

  return (
    <div className="tournament-stage-editor">
      <div className="tournament-stage-editor__header">
        <h3>Poules handmatig invullen</h3>
        <p>Plaats elk geaccepteerd team in exact één groep. Dubbels zijn niet toegestaan.</p>
      </div>

      <div className="tournament-stage-editor__grid tournament-stage-editor__grid--teams">
        {teams.map((team) => (
          <label className="tournament-stage-editor__field" key={team.id}>
            <span>{team.name}</span>
            <select
              value={groupByTeamId.get(team.id) ?? ""}
              onChange={(event) => {
                const nextGroupKey = event.target.value;

                onChange(
                  groups.map((group) => {
                    const withoutTeam = group.teamIds.filter((teamId) => teamId !== team.id);

                    if (group.key !== nextGroupKey) {
                      return {
                        ...group,
                        teamIds: withoutTeam,
                      };
                    }

                    return {
                      ...group,
                      teamIds: [...withoutTeam, team.id],
                    };
                  }),
                );
              }}
            >
              <option value="">Selecteer een groep</option>
              {groups.map((group) => (
                <option key={group.key} value={group.key}>
                  Groep {group.label}
                </option>
              ))}
            </select>
          </label>
        ))}
      </div>

      <div className="tournament-stage-groups">
        {groups.map((group) => (
          <div className="tournament-stage-groups__card" key={group.key}>
            <h4>Groep {group.label}</h4>
            {group.teamIds.length > 0 ? (
              <ul>
                {group.teamIds.map((teamId) => {
                  const team = teams.find((item) => item.id === teamId);
                  return <li key={teamId}>{team?.name ?? `#${teamId}`}</li>;
                })}
              </ul>
            ) : (
              <p>Lege groep</p>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
