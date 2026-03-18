import { getTournamentStatus } from "./tournamentStatus";

export type TournamentItem = {
  id: number | string;
  slug?: string;
  created_at?: string;
  name: string;
  status: string;
  game_igdb_id: number | string | null;
  format: { name: string } | number | string | null;
  players_per_team: number;
  start_date: string;
  end_date: string;
  current_teams: number;
  max_teams: number;
  user_id?: string;
  privacy?: string;
  winner_team_id?: number | null;
};

export type TournamentFilters = {
  game: string;
  status: string;
  date: string;
};

function isSameDay(dateA: Date, dateB: Date) {
  return (
    dateA.getFullYear() === dateB.getFullYear() &&
    dateA.getMonth() === dateB.getMonth() &&
    dateA.getDate() === dateB.getDate()
  );
}

export function filterTournaments(
  tournaments: TournamentItem[],
  filters: TournamentFilters,
) {
  return tournaments.filter((tournament) => {
    if (
      filters.game &&
      String(tournament.game_igdb_id ?? "").toLowerCase() !== filters.game.toLowerCase()
    ) {
      return false;
    }

    if (
      filters.status &&
      getTournamentStatus(tournament).toLowerCase() !== filters.status.toLowerCase()
    ) {
      return false;
    }

    if (filters.date) {
      const selectedDate = new Date(filters.date);
      const tournamentStartDate = new Date(tournament.start_date);
      if (Number.isNaN(selectedDate.getTime())) return false;
      if (Number.isNaN(tournamentStartDate.getTime())) return false;
      if (!isSameDay(selectedDate, tournamentStartDate)) return false;
    }

    return true;
  });
}
