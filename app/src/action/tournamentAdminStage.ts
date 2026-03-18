import { supabase } from "../lib/supabaseClient";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type TournamentStageType = "pending" | "bracket" | "groups_to_bracket";
export type TournamentStageMode = "automatic" | "manual";

export type TournamentAdminTeam = {
  id: number;
  name: string;
  status: "pending" | "accepted" | "rejected";
  captain_id: string;
  created_at: string;
};

export type TournamentAdminStageSummary = {
  id: number;
  name: string;
  slug: string;
  user_id: string;
  format: number | null;
  final_format: number | null;
  current_teams: number;
  min_teams: number;
  max_teams: number;
  players_per_team: number;
  tournament_type: TournamentStageType;
};

export type GroupDefinition = {
  key: string;
  label: string;
  teamIds: number[];
};

export type BracketSlot = {
  slotIndex: number;
  teamId: number | null;
};

export type BracketMatch = {
  roundNumber: number;
  matchNumber: number;
  team1Id: number | null;
  team2Id: number | null;
};

export type ExistingGroup = {
  id: number;
  name: string;
  position: number | null;
  teams: TournamentAdminTeam[];
};

export type ExistingMatch = {
  id: number;
  roundNumber: number;
  matchNumber: number;
  team1Id: number | null;
  team2Id: number | null;
  winnerId: number | null;
  team1Score: number;
  team2Score: number;
};

export type TournamentAdminStageData = {
  tournament: TournamentAdminStageSummary | null;
  acceptedTeams: TournamentAdminTeam[];
  existingGroups: ExistingGroup[];
  existingMatches: ExistingMatch[];
  error: string | null;
};

type MutationResult = {
  ok: boolean;
  message: string;
};

type MatchResultMutationResult = MutationResult & {
  match?: ExistingMatch;
};

type SaveBracketStageInput = {
  tournamentId: number;
  acceptedTeams: TournamentAdminTeam[];
  mode: TournamentStageMode;
  manualSlots?: BracketSlot[];
  t: Translate;
};

type SaveGroupsStageInput = {
  tournamentId: number;
  acceptedTeams: TournamentAdminTeam[];
  mode: TournamentStageMode;
  manualGroups?: GroupDefinition[];
  t: Translate;
};

type SupabaseGroupRow = {
  id: number;
  name?: string | null;
  position?: number | null;
};

type SupabaseGroupTeamRow = {
  group_id: number;
  team_id: number;
};

type SupabaseMatchRow = {
  id: number;
  round?: number | null;
  match_number?: number | null;
  team1_id?: number | null;
  team2_id?: number | null;
  winner_id?: number | null;
  team1_score?: number | null;
  team2_score?: number | null;
};

export async function loadTournamentAdminStageData(
  slug: string,
  t: Translate,
): Promise<TournamentAdminStageData> {
  const { data: tournamentData, error: tournamentError } = await supabase
    .from("tournaments")
    .select("*")
    .eq("slug", slug)
    .limit(1)
    .maybeSingle();

  if (tournamentError || !tournamentData) {
    return {
      tournament: null,
      acceptedTeams: [],
      existingGroups: [],
      existingMatches: [],
      error: tournamentError?.message ?? t("tournamentAdminStage.messages.tournamentNotFound"),
    };
  }

  const tournament = mapTournamentSummary(tournamentData);

  const { data: teamsData, error: teamsError } = await supabase
    .from("teams")
    .select("id, name, status, captain_id, created_at")
    .eq("tournament_id", tournament.id)
    .order("created_at", { ascending: true });

  if (teamsError) {
    return {
      tournament,
      acceptedTeams: [],
      existingGroups: [],
      existingMatches: [],
      error: teamsError.message,
    };
  }

  const acceptedTeams = ((teamsData as TournamentAdminTeam[] | null) ?? []).filter(
    (team) => team.status === "accepted",
  );

  const [groupsResult, matchesResult] = await Promise.all([
    loadExistingGroups(tournament.id, acceptedTeams),
    loadExistingMatches(tournament.id),
  ]);

  return {
    tournament,
    acceptedTeams,
    existingGroups: groupsResult,
    existingMatches: matchesResult,
    error: null,
  };
}

export function canLaunchTournamentStage(
  tournament: TournamentAdminStageSummary,
  acceptedTeamsCount: number,
) {
  const enoughTeams = acceptedTeamsCount >= tournament.min_teams;
  const structureExists = tournament.tournament_type !== "pending";

  return {
    enoughTeams,
    structureExists,
    canLaunch: enoughTeams && !structureExists,
  };
}

export function getRecommendedGroupCount(teamCount: number): number {
  if (teamCount <= 4) {
    return 2;
  }

  if (teamCount <= 8) {
    return 2;
  }

  if (teamCount <= 16) {
    return 4;
  }

  if (teamCount <= 32) {
    return 8;
  }

  return 16;
}

export function createInitialGroups(teamCount: number): GroupDefinition[] {
  const groupCount = getRecommendedGroupCount(teamCount);

  return Array.from({ length: groupCount }, (_, index) => ({
    key: `group-${index + 1}`,
    label: String.fromCharCode(65 + index),
    teamIds: [],
  }));
}

export function createAutomaticGroups(teams: TournamentAdminTeam[]): GroupDefinition[] {
  const groups = createInitialGroups(teams.length);

  teams.forEach((team, index) => {
    const groupIndex = index % groups.length;
    groups[groupIndex] = {
      ...groups[groupIndex],
      teamIds: [...groups[groupIndex].teamIds, team.id],
    };
  });

  return groups;
}

export function getBracketSize(teamCount: number): number {
  let size = 2;

  while (size < teamCount) {
    size *= 2;
  }

  return size;
}

export function createEmptyBracketSlots(teamCount: number): BracketSlot[] {
  const bracketSize = getBracketSize(Math.max(teamCount, 2));

  return Array.from({ length: bracketSize }, (_, index) => ({
    slotIndex: index,
    teamId: null,
  }));
}

export function createAutomaticBracketSlots(
  teams: TournamentAdminTeam[],
): BracketSlot[] {
  const slots = createEmptyBracketSlots(teams.length);

  teams.forEach((team, index) => {
    slots[index] = {
      slotIndex: index,
      teamId: team.id,
    };
  });

  return slots;
}

export function buildBracketMatchesFromSlots(slots: BracketSlot[]): BracketMatch[] {
  const bracketSize = slots.length;
  const rounds = Math.log2(bracketSize);
  const matches: BracketMatch[] = [];

  for (let roundNumber = 1; roundNumber <= rounds; roundNumber += 1) {
    const matchesInRound = bracketSize / 2 ** roundNumber;

    for (let matchNumber = 1; matchNumber <= matchesInRound; matchNumber += 1) {
      if (roundNumber === 1) {
        const slotBase = (matchNumber - 1) * 2;
        matches.push({
          roundNumber,
          matchNumber,
          team1Id: slots[slotBase]?.teamId ?? null,
          team2Id: slots[slotBase + 1]?.teamId ?? null,
        });
        continue;
      }

      matches.push({
        roundNumber,
        matchNumber,
        team1Id: null,
        team2Id: null,
      });
    }
  }

  return matches;
}

export function validateManualBracket(
  slots: BracketSlot[],
  teams: TournamentAdminTeam[],
  t: Translate,
): string | null {
  const assignedTeamIds = slots
    .map((slot) => slot.teamId)
    .filter((teamId): teamId is number => teamId !== null);

  const uniqueTeamIds = new Set(assignedTeamIds);
  if (uniqueTeamIds.size !== assignedTeamIds.length) {
    return t("tournamentAdminStage.messages.duplicateTeams");
  }

  if (assignedTeamIds.length !== teams.length) {
    return t("tournamentAdminStage.messages.allTeamsMustBeAssigned");
  }

  return null;
}

export function validateManualGroups(
  groups: GroupDefinition[],
  teams: TournamentAdminTeam[],
  t: Translate,
): string | null {
  const assignedTeamIds = groups.flatMap((group) => group.teamIds);
  const uniqueTeamIds = new Set(assignedTeamIds);

  if (uniqueTeamIds.size !== assignedTeamIds.length) {
    return t("tournamentAdminStage.messages.duplicateTeams");
  }

  if (assignedTeamIds.length !== teams.length) {
    return t("tournamentAdminStage.messages.allTeamsMustBeAssigned");
  }

  return null;
}

export async function saveBracketStage({
  tournamentId,
  acceptedTeams,
  mode,
  manualSlots,
  t,
}: SaveBracketStageInput): Promise<MutationResult> {
  const slots =
    mode === "automatic"
      ? createAutomaticBracketSlots(acceptedTeams)
      : manualSlots ?? [];

  const validationError = validateManualBracket(slots, acceptedTeams, t);
  if (validationError) {
    return {
      ok: false,
      message: validationError,
    };
  }

  const matches = buildBracketMatchesFromSlots(slots).map((match) => ({
    tournament_id: tournamentId,
    round: match.roundNumber,
    match_number: match.matchNumber,
    team1_id: match.team1Id,
    team2_id: match.team2Id,
  }));

  const { error: matchesError } = await supabase.from("matches").insert(matches);
  if (matchesError) {
    return {
      ok: false,
      message: mapPersistenceError(matchesError.message, t),
    };
  }

  const { error: tournamentError } = await supabase
    .from("tournaments")
    .update({ tournament_type: "bracket" })
    .eq("id", tournamentId);

  if (tournamentError) {
    return {
      ok: false,
      message: mapPersistenceError(tournamentError.message, t),
    };
  }

  return {
    ok: true,
    message: t("tournamentAdminStage.messages.bracketCreated"),
  };
}

export async function saveGroupsStage({
  tournamentId,
  acceptedTeams,
  mode,
  manualGroups,
  t,
}: SaveGroupsStageInput): Promise<MutationResult> {
  const groups =
    mode === "automatic"
      ? createAutomaticGroups(acceptedTeams)
      : manualGroups ?? [];

  const validationError = validateManualGroups(groups, acceptedTeams, t);
  if (validationError) {
    return {
      ok: false,
      message: validationError,
    };
  }

  const { data: insertedGroups, error: groupsError } = await supabase
    .from("groups")
    .insert(
      groups.map((group, index) => ({
        tournament_id: tournamentId,
        name: group.label,
        position: index + 1,
      })),
    )
    .select("id, name, position");

  if (groupsError || !insertedGroups) {
    return {
      ok: false,
      message: mapPersistenceError(groupsError?.message, t),
    };
  }

  const groupTeamRows = groups.flatMap((group) => {
    const insertedGroup = (insertedGroups as SupabaseGroupRow[]).find(
      (item) => (item.name ?? "").toUpperCase() === group.label.toUpperCase(),
    );

    if (!insertedGroup) {
      return [];
    }

    return group.teamIds.map((teamId, index) => ({
      group_id: insertedGroup.id,
      team_id: teamId,
      seed: index + 1,
    }));
  });

  const { error: groupTeamsError } = await supabase
    .from("group_teams")
    .insert(groupTeamRows);

  if (groupTeamsError) {
    return {
      ok: false,
      message: mapPersistenceError(groupTeamsError.message, t),
    };
  }

  const { error: tournamentError } = await supabase
    .from("tournaments")
    .update({ tournament_type: "groups_to_bracket" })
    .eq("id", tournamentId);

  if (tournamentError) {
    return {
      ok: false,
      message: mapPersistenceError(tournamentError.message, t),
    };
  }

  return {
    ok: true,
    message: t("tournamentAdminStage.messages.groupsCreated"),
  };
}

function mapTournamentSummary(data: Record<string, unknown>): TournamentAdminStageSummary {
  const tournamentType = normalizeTournamentType(data.tournament_type);

  return {
    id: Number(data.id),
    name: String(data.name ?? ""),
    slug: String(data.slug ?? ""),
    user_id: String(data.user_id ?? ""),
    format: data.format ? Number(data.format) : null,
    final_format: data.final_format ? Number(data.final_format) : null,
    current_teams: Number(data.current_teams ?? 0),
    min_teams: Number(data.min_teams ?? 0),
    max_teams: Number(data.max_teams ?? 0),
    players_per_team: Number(data.players_per_team ?? 0),
    tournament_type: tournamentType,
  };
}

function normalizeTournamentType(value: unknown): TournamentStageType {
  if (
    value === "bracket" ||
    value === "groups_to_bracket" ||
    value === "pending"
  ) {
    return value;
  }

  return "pending";
}

async function loadExistingGroups(
  tournamentId: number,
  acceptedTeams: TournamentAdminTeam[],
): Promise<ExistingGroup[]> {
  const { data: groupsData, error: groupsError } = await supabase
    .from("groups")
    .select("id, name, position")
    .eq("tournament_id", tournamentId)
    .order("position", { ascending: true });

  if (groupsError || !groupsData) {
    if (isMissingRelationError(groupsError?.message)) {
      return [];
    }

    return [];
  }

  const groups = groupsData as SupabaseGroupRow[];
  if (groups.length === 0) {
    return [];
  }

  const { data: groupTeamsData, error: groupTeamsError } = await supabase
    .from("group_teams")
    .select("group_id, team_id")
    .in(
      "group_id",
      groups.map((group) => group.id),
    );

  if (groupTeamsError || !groupTeamsData) {
    return [];
  }

  const teamMap = new Map(acceptedTeams.map((team) => [team.id, team]));
  const groupTeams = groupTeamsData as SupabaseGroupTeamRow[];

  return groups.map((group) => ({
    id: group.id,
    name: group.name ?? "?",
    position: group.position ?? null,
    teams: groupTeams
      .filter((item) => item.group_id === group.id)
      .map((item) => teamMap.get(item.team_id))
      .filter((team): team is TournamentAdminTeam => Boolean(team)),
  }));
}

async function loadExistingMatches(tournamentId: number): Promise<ExistingMatch[]> {
  const { data: matchesData, error: matchesError } = await supabase
    .from("matches")
    .select("id, round, match_number, team1_id, team2_id, winner_id, team1_score, team2_score")
    .eq("tournament_id", tournamentId)
    .order("round", { ascending: true })
    .order("match_number", { ascending: true });

  if (matchesError || !matchesData) {
    if (isMissingRelationError(matchesError?.message)) {
      return [];
    }

    return [];
  }

  return (matchesData as SupabaseMatchRow[]).map((match) => ({
    id: match.id,
    roundNumber: Number(match.round ?? 1),
    matchNumber: Number(match.match_number ?? 1),
    team1Id: match.team1_id ?? null,
    team2Id: match.team2_id ?? null,
    winnerId: match.winner_id ?? null,
    team1Score: Number(match.team1_score ?? 0),
    team2Score: Number(match.team2_score ?? 0),
  }));
}

function isMissingRelationError(message?: string | null): boolean {
  return typeof message === "string" && message.toLowerCase().includes("relation");
}

function mapPersistenceError(message: string | null | undefined, t: Translate): string {
  if (!message) {
    return t("tournamentAdminStage.messages.saveFailed");
  }

  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes("relation")) {
    return t("tournamentAdminStage.messages.missingStageTables");
  }

  if (normalizedMessage.includes("column")) {
    return t("tournamentAdminStage.messages.missingStageColumns");
  }

  return message;
}

export function getWinsNeededFromFormatName(formatName: string): number {
  const parsedBestOf = Number(formatName.replace(/\D/g, ""));

  if (!Number.isFinite(parsedBestOf) || parsedBestOf <= 0) {
    return 1;
  }

  return Math.ceil(parsedBestOf / 2);
}

export function getWinsNeededForMatch(
  matchRound: number,
  maxRound: number,
  defaultFormatName: string,
  finalFormatName: string | null,
): number {
  if (finalFormatName && matchRound === maxRound) {
    return getWinsNeededFromFormatName(finalFormatName);
  }

  return getWinsNeededFromFormatName(defaultFormatName);
}

export function isValidMatchScore(
  team1Score: number,
  team2Score: number,
  winsNeeded: number,
): boolean {
  if (
    !Number.isInteger(team1Score) ||
    !Number.isInteger(team2Score) ||
    team1Score < 0 ||
    team2Score < 0 ||
    team1Score > winsNeeded ||
    team2Score > winsNeeded
  ) {
    return false;
  }

  if (team1Score === winsNeeded && team2Score === winsNeeded) {
    return false;
  }

  return true;
}

export async function saveMatchResult(
  apiUrl: string,
  slug: string,
  matchId: number,
  team1Score: number,
  team2Score: number,
  accessToken: string,
  t: Translate,
): Promise<MatchResultMutationResult> {
  try {
    const response = await fetch(
      `${apiUrl}/api/tournaments/${encodeURIComponent(slug)}/admin/stage/matches/${matchId}/result`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({
          team1_score: team1Score,
          team2_score: team2Score,
        }),
      },
    );

    const result = await response.json().catch(() => null);
    if (!response.ok) {
      return {
        ok: false,
        message:
          (typeof result?.message === "string" && result.message) ||
          "Unable to save the match result.",
      };
    }

    return {
      ok: true,
      message:
        (typeof result?.message === "string" && result.message) ||
        "Match result saved successfully.",
      match: result?.match
        ? {
            id: Number(result.match.id),
            roundNumber: Number(result.match.round ?? 1),
            matchNumber: Number(result.match.match_number ?? 1),
            team1Id: result.match.team1_id ?? null,
            team2Id: result.match.team2_id ?? null,
            winnerId: result.match.winner_id ?? null,
            team1Score: Number(result.match.team1_score ?? 0),
            team2Score: Number(result.match.team2_score ?? 0),
          }
        : undefined,
    };
  } catch {
    return {
      ok: false,
      message: t("tournamentAdmin.messages.connectionFailed"),
    };
  }
}
