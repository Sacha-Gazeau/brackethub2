import { supabase } from "../lib/supabaseClient";
import { getTournamentStatus } from "./tournamentStatus";
import {
  loadTournamentList,
  resolveTournamentGameRefs,
  TOURNAMENT_LIST_SELECT,
} from "./tournamentData";
import type { TournamentItem } from "./tournamentFilter";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type ProfileSummary = {
  username: string | null;
  avatar: string | null;
  coins: number;
  lifetimecoins: number | null;
  email?: string | null;
};

export type ProfileCreatedTournament = TournamentItem;

export type ProfileJoinedTournament = {
  id: number;
  name: string;
  slug: string | undefined;
  gameName: string;
  teamName: string;
  status: ReturnType<typeof getTournamentStatus>;
  participants: string;
};

export type ProfileBetHistoryItem = {
  id: number;
  tournamentName: string;
  tournamentSlug?: string;
  prediction: string;
  stake: number;
  result: "won" | "lost" | "pending";
  payout: number | null;
};

type TeamMembershipRow = {
  id: number;
  name: string;
  tournament_id: number;
  status: string;
};

type BetRow = {
  id: number;
  tournament_id: number;
  team_id: number;
  coins_bet: number;
  status: "won" | "lost" | "pending";
  paid_out: boolean;
};

export type ProfilePageData = {
  profile: ProfileSummary | null;
  createdTournaments: ProfileCreatedTournament[];
  joinedTournaments: ProfileJoinedTournament[];
  bets: ProfileBetHistoryItem[];
};

export async function loadProfilePageData(
  userId: string,
  apiUrl: string | undefined,
  t: Translate,
): Promise<ProfilePageData> {
  const [
    profileResponse,
    createdTournaments,
    joinedTeamsResponse,
    betsResponse,
  ] = await Promise.all([
    supabase
      .from("profiles")
      .select("username, avatar, coins, lifetimecoins, email")
      .eq("id", userId)
      .maybeSingle(),
    loadTournamentList({ scope: "mine", userId }),
    supabase
      .from("teams")
      .select("id, name, tournament_id, status")
      .eq("captain_id", userId)
      .eq("status", "accepted")
      .order("created_at", { ascending: false }),
    supabase
      .from("bets")
      .select("id, tournament_id, team_id, coins_bet, status, paid_out")
      .eq("user_id", userId)
      .order("created_at", { ascending: false }),
  ]);

  if (profileResponse.error) {
    throw profileResponse.error;
  }

  const profile = (profileResponse.data as ProfileSummary | null) ?? null;
  const joinedTeams = (joinedTeamsResponse.data as TeamMembershipRow[] | null) ?? [];

  const joinedTournamentIds = [
    ...new Set(joinedTeams.map((team) => team.tournament_id)),
  ];
  const joinedTournamentResponse =
    joinedTournamentIds.length > 0
      ? await supabase
          .from("tournaments")
          .select(TOURNAMENT_LIST_SELECT)
          .in("id", joinedTournamentIds)
      : { data: [] as TournamentItem[], error: null };

  if (joinedTournamentResponse.error) {
    throw joinedTournamentResponse.error;
  }

  const joinedTournamentRows =
    (joinedTournamentResponse.data as TournamentItem[] | null) ?? [];
  const joinedGameRefs = await resolveTournamentGameRefs(
    apiUrl,
    joinedTournamentRows,
    t,
  );

  const joinedTournamentById = new Map(
    joinedTournamentRows.map((tournament) => [Number(tournament.id), tournament]),
  );
  const joinedGameNameById = new Map(
    joinedGameRefs.map((game) => [String(game.id), game.name]),
  );

  const joinedTournaments = joinedTeams.flatMap((team) => {
      const tournament = joinedTournamentById.get(team.tournament_id);
      if (!tournament) {
        return [];
      }

      return [{
        id: Number(tournament.id),
        name: tournament.name,
        slug: tournament.slug,
        gameName:
          joinedGameNameById.get(String(tournament.game_igdb_id ?? "")) ?? "-",
        teamName: team.name,
        status: getTournamentStatus(tournament),
        participants: `${tournament.current_teams}/${tournament.max_teams}`,
      }];
    });

  const createdTournamentMap = new Map(
    createdTournaments.map((tournament) => [Number(tournament.id), tournament]),
  );

  const bets = await mapProfileBets(
    (betsResponse.data as BetRow[] | null) ?? [],
    createdTournamentMap,
  );

  return {
    profile,
    createdTournaments,
    joinedTournaments,
    bets,
  };
}

export function getProfileBetWinRate(bets: ProfileBetHistoryItem[]) {
  const settledBets = bets.filter((bet) => bet.result !== "pending");
  if (settledBets.length === 0) {
    return 0;
  }

  const wonBets = settledBets.filter((bet) => bet.result === "won").length;
  return Math.round((wonBets / settledBets.length) * 100);
}

export function getProfileSpentCoins(bets: ProfileBetHistoryItem[]) {
  return bets.reduce((total, bet) => total + bet.stake, 0);
}

async function mapProfileBets(
  bets: BetRow[],
  knownTournamentMap: Map<number, TournamentItem>,
) {
  if (bets.length === 0) {
    return [] as ProfileBetHistoryItem[];
  }

  const tournamentIds = [
    ...new Set(
      bets
        .map((bet) => bet.tournament_id)
        .filter((tournamentId) => !knownTournamentMap.has(tournamentId)),
    ),
  ];
  const teamIds = [...new Set(bets.map((bet) => bet.team_id))];

  const [extraTournamentsResponse, teamsResponse] = await Promise.all([
    tournamentIds.length > 0
      ? supabase
          .from("tournaments")
          .select("id, name, slug")
          .in("id", tournamentIds)
      : { data: [] as { id: number; name: string; slug?: string }[], error: null },
    teamIds.length > 0
      ? supabase.from("teams").select("id, name").in("id", teamIds)
      : { data: [] as { id: number; name: string }[], error: null },
  ]);

  if (extraTournamentsResponse.error) {
    throw extraTournamentsResponse.error;
  }

  if (teamsResponse.error) {
    throw teamsResponse.error;
  }

  const tournamentNameById = new Map<number, { name: string; slug?: string }>(
    extraTournamentsResponse.data?.map((tournament) => [
      Number(tournament.id),
      { name: tournament.name, slug: tournament.slug },
    ]) ?? [],
  );

  for (const [id, tournament] of knownTournamentMap.entries()) {
    tournamentNameById.set(id, {
      name: tournament.name,
      slug: tournament.slug,
    });
  }

  const teamNameById = new Map<number, string>(
    (teamsResponse.data ?? []).map((team) => [Number(team.id), team.name]),
  );

  return bets.map((bet) => ({
    id: bet.id,
    tournamentName:
      tournamentNameById.get(bet.tournament_id)?.name ?? "Onbekend toernooi",
    tournamentSlug: tournamentNameById.get(bet.tournament_id)?.slug,
    prediction: teamNameById.get(bet.team_id) ?? "Onbekend team",
    stake: bet.coins_bet,
    result: bet.status,
    payout: bet.status === "won" && bet.paid_out ? bet.coins_bet * 2 : null,
  }));
}
