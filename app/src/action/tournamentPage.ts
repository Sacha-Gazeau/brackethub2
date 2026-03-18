import { supabase } from "../lib/supabaseClient";
import { getTournamentStatus } from "./tournamentStatus";
import type { TournamentFilters, TournamentItem } from "./tournamentFilter";
import {
  loadTournamentList,
  resolveTournamentGameRefs,
} from "./tournamentData";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export const initialTournamentFilters: TournamentFilters = {
  game: "",
  status: "",
  date: "",
};

export async function loadTournamentPageSession() {
  const { data } = await supabase.auth.getSession();
  return data.session?.user?.id ?? null;
}

export async function loadTournamentPageTournaments(
  isMineScope: boolean,
  userId: string | null,
) {
  return loadTournamentList({
    scope: isMineScope ? "mine" : "public",
    userId,
  });
}

export async function loadTournamentPageGames(
  apiUrl: string | undefined,
  tournaments: TournamentItem[],
  t: Translate,
) {
  return resolveTournamentGameRefs(apiUrl, tournaments, t);
}

export function selectVisibleTournaments(
  tournaments: TournamentItem[],
  isMineScope: boolean,
  userId: string | null,
) {
  if (isMineScope) {
    return tournaments.filter((tournament) => tournament.user_id === userId);
  }

  return tournaments.filter(
    (tournament) =>
      tournament.privacy === "public" || tournament.privacy === "official",
  );
}

export function selectGameOptions(tournaments: TournamentItem[]) {
  return [
    ...new Set(
      tournaments
        .map((tournament) => String(tournament.game_igdb_id ?? ""))
        .filter((gameId) => gameId),
    ),
  ];
}

export function selectStatusOptions(tournaments: TournamentItem[]) {
  return [...new Set(tournaments.map((tournament) => getTournamentStatus(tournament)))];
}
