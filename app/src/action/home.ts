import { supabase } from "../lib/supabaseClient";
import type { TournamentItem } from "./tournamentFilter";
import { getTournamentStatus } from "./tournamentStatus";
import { loadTournamentList } from "./tournamentData";

export type HomeProfile = {
  coins: number;
  lifetimecoins: number | null;
  username: string | null;
};

export async function loadHomeSession() {
  const { data } = await supabase.auth.getSession();

  return {
    isLogin: !!data.session,
    userId: data.session?.user?.id ?? null,
  };
}

export function subscribeHomeSession(
  onSessionChange: (next: { isLogin: boolean; userId: string | null }) => void,
) {
  const { data } = supabase.auth.onAuthStateChange((_event, session) => {
    onSessionChange({
      isLogin: !!session,
      userId: session?.user?.id ?? null,
    });
  });

  return () => {
    data.subscription.unsubscribe();
  };
}

export async function loadHomeProfile(userId: string) {
  const { data, error } = await supabase
    .from("profiles")
    .select("username, coins, lifetimecoins")
    .eq("id", userId)
    .maybeSingle();

  if (error) {
    throw error;
  }

  if (!data) {
    return null;
  }

  return data as HomeProfile;
}

export async function loadHomeTournaments() {
  return loadTournamentList({ scope: "all" });
}

function sortByCreatedAtDesc(left: TournamentItem, right: TournamentItem) {
  return (
    new Date(right.created_at ?? 0).getTime() -
    new Date(left.created_at ?? 0).getTime()
  );
}

function sortByStartDateAsc(left: TournamentItem, right: TournamentItem) {
  return new Date(left.start_date).getTime() - new Date(right.start_date).getTime();
}

export function selectMyLatestTournaments(
  tournaments: TournamentItem[],
  userId: string | null,
) {
  return tournaments
    .filter((tournament) => tournament.user_id === userId)
    .sort(sortByCreatedAtDesc)
    .slice(0, 3);
}

export function selectLatestOfficialTournaments(tournaments: TournamentItem[]) {
  return tournaments
    .filter((tournament) => tournament.privacy === "official")
    .sort(sortByCreatedAtDesc)
    .slice(0, 3);
}

export function selectUpcomingOfficialTournaments(tournaments: TournamentItem[]) {
  return tournaments
    .filter(
      (tournament) =>
        tournament.privacy === "official" &&
        getTournamentStatus(tournament) === "aankomend",
    )
    .sort(sortByStartDateAsc)
    .slice(0, 3);
}
