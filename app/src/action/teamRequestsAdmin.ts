import { supabase } from "../lib/supabaseClient";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type TeamRequestStatus = "pending" | "accepted" | "rejected";

export type TeamRequestCaptain = {
  id: string;
  email: string;
  username: string | null;
  avatar: string | null;
  name: string | null;
};

export type TeamRequestMember = {
  id: number;
  name: string;
  team_id: number;
  created_at: string;
};

export type TeamRequestItem = {
  id: number;
  name: string;
  tournament_id: number;
  captain_id: string;
  status: TeamRequestStatus;
  rejection_reason: string | null;
  created_at: string;
  captain: TeamRequestCaptain | null;
  players: TeamRequestMember[];
};

export type TournamentAdminSummary = {
  id: number;
  name: string;
  user_id: string;
  players_per_team: number;
};

type TeamRow = Omit<TeamRequestItem, "captain" | "players">;

type LoadTournamentAdminTeamRequestsResult = {
  tournament: TournamentAdminSummary | null;
  teams: TeamRequestItem[];
  error: string | null;
};

type TeamRequestActionResult = {
  ok: boolean;
  message: string;
};

export async function loadTournamentAdminTeamRequests(
  slug: string,
): Promise<LoadTournamentAdminTeamRequestsResult> {
  const { data: tournament, error: tournamentError } = await supabase
    .from("tournaments")
    .select("id, name, user_id, players_per_team")
    .eq("slug", slug)
    .limit(1)
    .maybeSingle();

  if (tournamentError || !tournament) {
    return {
      tournament: null,
      teams: [],
      error: tournamentError?.message ?? "Tournament not found.",
    };
  }

  const { data: teamsData, error: teamsError } = await supabase
    .from("teams")
    .select("id, name, tournament_id, captain_id, status, rejection_reason, created_at")
    .eq("tournament_id", tournament.id)
    .order("created_at", { ascending: true });

  if (teamsError) {
    return {
      tournament: tournament as TournamentAdminSummary,
      teams: [],
      error: teamsError.message,
    };
  }

  const teams = (teamsData as TeamRow[] | null) ?? [];
  if (teams.length === 0) {
    return {
      tournament: tournament as TournamentAdminSummary,
      teams: [],
      error: null,
    };
  }

  const teamIds = teams.map((team) => team.id);
  const captainIds = [...new Set(teams.map((team) => team.captain_id))];

  const [{ data: membersData, error: membersError }, { data: profilesData, error: profilesError }] =
    await Promise.all([
      supabase
        .from("team_members")
        .select("id, name, team_id, created_at")
        .in("team_id", teamIds)
        .order("created_at", { ascending: true }),
      supabase
        .from("profiles")
        .select("id, email, username, avatar, name")
        .in("id", captainIds),
    ]);

  if (membersError || profilesError) {
    return {
      tournament: tournament as TournamentAdminSummary,
      teams: [],
      error:
        membersError?.message ??
        profilesError?.message ??
        "Failed to load team request details.",
    };
  }

  const playersByTeamId = new Map<number, TeamRequestMember[]>();
  for (const member of ((membersData as TeamRequestMember[] | null) ?? [])) {
    const currentMembers = playersByTeamId.get(member.team_id) ?? [];
    currentMembers.push(member);
    playersByTeamId.set(member.team_id, currentMembers);
  }

  const captainById = new Map<string, TeamRequestCaptain>();
  for (const profile of ((profilesData as TeamRequestCaptain[] | null) ?? [])) {
    captainById.set(profile.id, profile);
  }

  return {
    tournament: tournament as TournamentAdminSummary,
    teams: teams.map((team) => ({
      ...team,
      captain: captainById.get(team.captain_id) ?? null,
      players: playersByTeamId.get(team.id) ?? [],
    })),
    error: null,
  };
}

export async function acceptTeamRequest(
  apiUrl: string,
  teamId: number,
  t: Translate,
): Promise<TeamRequestActionResult> {
  return submitTeamRequestAction(
    `${apiUrl}/api/team-requests/${teamId}/accept`,
    {
      method: "POST",
    },
    t,
  );
}

export async function rejectTeamRequest(
  apiUrl: string,
  teamId: number,
  rejectionReason: string,
  t: Translate,
): Promise<TeamRequestActionResult> {
  return submitTeamRequestAction(
    `${apiUrl}/api/team-requests/${teamId}/reject`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        rejectionReason,
      }),
    },
    t,
  );
}

export async function createAdminTeam(
  apiUrl: string,
  payload: {
    tournamentId: number;
    captainId: string;
    teamName: string;
    playerNames: string[];
  },
  t: Translate,
): Promise<TeamRequestActionResult> {
  return submitTeamRequestAction(
    `${apiUrl}/api/team-requests/admin`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    },
    t,
  );
}

async function submitTeamRequestAction(
  url: string,
  options: RequestInit,
  t: Translate,
): Promise<TeamRequestActionResult> {
  try {
    const response = await fetch(url, options);
    const result = await response.json().catch(() => null);

    if (!response.ok) {
      return {
        ok: false,
        message: mapTeamRequestAdminMessage(
          typeof result?.message === "string" ? result.message : null,
          t,
        ),
      };
    }

    return {
      ok: true,
      message:
        mapTeamRequestAdminMessage(
          typeof result?.message === "string" ? result.message : null,
          t,
        ) ?? t("tournamentAdmin.messages.actionSuccess"),
    };
  } catch {
    return {
      ok: false,
      message: t("tournamentAdmin.messages.connectionFailed"),
    };
  }
}

function mapTeamRequestAdminMessage(
  message: string | null,
  t: Translate,
): string {
  if (!message) {
    return t("tournamentAdmin.messages.actionFailed");
  }

  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes("tournament not found")) {
    return t("tournamentAdmin.messages.tournamentNotFound");
  }

  if (normalizedMessage.includes("tournament is full")) {
    return t("tournamentAdmin.messages.tournamentFull");
  }

  if (normalizedMessage.includes("player count")) {
    return t("tournamentAdmin.messages.invalidPlayerCount");
  }

  if (normalizedMessage.includes("team accepted successfully")) {
    return t("tournamentAdmin.messages.teamAccepted");
  }

  if (normalizedMessage.includes("team rejected successfully")) {
    return t("tournamentAdmin.messages.teamRejected");
  }

  if (normalizedMessage.includes("accepted team created successfully")) {
    return t("tournamentAdmin.messages.adminTeamCreated");
  }

  return message;
}
