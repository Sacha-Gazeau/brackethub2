export type TournamentStatus = "aankomend" | "live" | "finished";

export type TournamentStatusSource = {
  start_date: string;
  end_date: string;
  status?: string | null;
};

export function getTournamentStatus(
  tournament: TournamentStatusSource,
): TournamentStatus {
  if (
    tournament.status === "aankomend" ||
    tournament.status === "live" ||
    tournament.status === "finished"
  ) {
    return tournament.status;
  }

  const now = Date.now();
  const startAt = Date.parse(tournament.start_date);
  const endAt = Date.parse(tournament.end_date);

  if (!Number.isNaN(endAt) && now > endAt) {
    return "finished";
  }

  if (!Number.isNaN(startAt) && now >= startAt) {
    return "live";
  }

  return "aankomend";
}
