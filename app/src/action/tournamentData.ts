import { supabase } from "../lib/supabaseClient";
import { getGameById } from "./games";
import type { TournamentItem } from "./tournamentFilter";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type TournamentScope = "all" | "mine" | "public";

export type GameRef = {
  id: number;
  name: string;
};

export const TOURNAMENT_LIST_SELECT = [
  "id",
  "slug",
  "created_at",
  "name",
  "status",
  "game_igdb_id",
  "format",
  "players_per_team",
  "start_date",
  "end_date",
  "current_teams",
  "max_teams",
  "user_id",
  "privacy",
  "winner_team_id",
].join(", ");

const gameRefCache = new Map<number, Promise<GameRef | null>>();

export async function loadTournamentList({
  scope,
  userId,
}: {
  scope: TournamentScope;
  userId?: string | null;
}) {
  if (scope === "mine" && !userId) {
    return [] as TournamentItem[];
  }

  let query = supabase.from("tournaments").select(TOURNAMENT_LIST_SELECT);

  if (scope === "mine" && userId) {
    query = query.eq("user_id", userId);
  }

  if (scope === "public") {
    query = query.or("privacy.eq.public,privacy.eq.official");
  }

  const { data, error } = await query;

  if (error) {
    throw error;
  }

  return ((data as unknown as TournamentItem[] | null) ?? []);
}

export async function resolveTournamentGameRefs(
  apiUrl: string | undefined,
  tournaments: TournamentItem[],
  t: Translate,
) {
  if (!apiUrl) {
    return [] as GameRef[];
  }

  const uniqueGameIds = [
    ...new Set(
      tournaments
        .map((tournament) => Number(tournament.game_igdb_id))
        .filter((gameId) => Number.isFinite(gameId) && gameId > 0),
    ),
  ];

  const gameResponses = await Promise.all(
    uniqueGameIds.map(async (gameId) => {
      if (!gameRefCache.has(gameId)) {
        gameRefCache.set(
          gameId,
          getGameById(apiUrl, gameId, t).then((response) =>
            response.data
              ? { id: response.data.id, name: response.data.name }
              : null,
          ),
        );
      }

      return gameRefCache.get(gameId) ?? Promise.resolve(null);
    }),
  );

  return gameResponses.flatMap((game) => (game ? [game] : []));
}
