import { startTransition, useEffect, useState } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { dateYear, formatDateYear, formatHours } from "../../action/date";
import { supabase } from "../../lib/supabaseClient";
import TournamentParticipate from "../../components/TournamentParticipate";
import TournamentBetModal from "../../components/TournamentBetModal";
import TournamentBracket from "../../components/TournamentBracket";
import { getGameById } from "../../action/games";
import { getTournamentStatus } from "../../action/tournamentStatus";
import {
  getPrivacyLabel,
  getRelationName,
} from "../../action/tournamentDetail";
import { loadTournamentBetState, type TournamentBet } from "../../action/tournamentBetting";
import { usePageSeo } from "../../action/usePageSeo";
import ControllerIcon from "../../assets/icon-controller.svg?react";
import CalendarIcon from "../../assets/icon-calendar.svg?react";
import ClockIcon from "../../assets/icon-clock.svg?react";
import UsersIcon from "../../assets/icon-users.svg?react";

type Tournament = {
  id: number | string;
  name: string;
  game_igdb_id: number | string | null;
  format: number | string | null;
  max_teams: number;
  min_teams: number;
  current_teams: number;
  players_per_team: number;
  start_date: string;
  end_date: string;
  user_id: string;
  status: string;
  final_format: number | string | null;
  description: string | null;
  privacy: string;
  tournament_type?: string;
  winner_team_id?: number | null;
};

type TournamentTeamMember = {
  id: number;
  name: string;
  team_id: number;
  created_at: string;
};

type TournamentTeam = {
  id: number;
  name: string;
  status: string;
  players: TournamentTeamMember[];
};

type TournamentMatch = {
  id: number;
  round: number;
  match_number: number;
  team1_id: number | null;
  team2_id: number | null;
  winner_id: number | null;
  team1_score: number;
  team2_score: number;
};

export default function TournamentDetails() {
  const { t } = useTranslation();
  const apiUrl = import.meta.env.VITE_API_URL;
  const { id: slug } = useParams<{ id: string }>();
  const location = useLocation();
  const activeTab = (location.hash || "#bracket").replace("#", "");

  const [tournament, setTournament] = useState<Tournament | null>(null);
  const [gameName, setGameName] = useState<string>("-");
  const [gameCoverUrl, setGameCoverUrl] = useState<string | null>(null);
  const [formatName, setFormatName] = useState<string>("-");
  const [finalFormatName, setFinalFormatName] = useState<string>("-");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [teams, setTeams] = useState<TournamentTeam[]>([]);
  const [matches, setMatches] = useState<TournamentMatch[]>([]);
  const [joinPopupOpen, setJoinPopupOpen] = useState(false);
  const [joinSuccessMessage, setJoinSuccessMessage] = useState<string | null>(
    null,
  );
  const [betSuccessMessage, setBetSuccessMessage] = useState<string | null>(null);
  const [betStateError, setBetStateError] = useState<string | null>(null);
  const [currentBet, setCurrentBet] = useState<TournamentBet | null>(null);
  const [coinsBalance, setCoinsBalance] = useState<number | null>(null);
  const [betModalTeam, setBetModalTeam] = useState<TournamentTeam | null>(null);
  const [userId, setUserId] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  usePageSeo({
    title: tournament?.name
      ? `BracketHub | ${tournament.name}`
      : "BracketHub | Tournament",
    description: tournament?.name
      ? `Bekijk teams, matches, brackets en betting voor ${tournament.name} op BracketHub.`
      : "Bekijk teams, matches, brackets en betting van dit toernooi op BracketHub.",
  });

  useEffect(() => {
    const loadTournament = async () => {
      if (!slug) {
        startTransition(() => {
          setError(t("tournamentDetail.messages.missingSlug"));
          setLoading(false);
        });
        return;
      }

      if (!apiUrl) {
        startTransition(() => {
          setError(t("createTournamentPage.messages.missingApiUrl"));
          setLoading(false);
        });
        return;
      }

      startTransition(() => {
        setLoading(true);
      });

      const tournamentResponse = await fetch(
        `${apiUrl}/api/tournaments/${encodeURIComponent(slug)}`,
      );
      const tournamentResult = await tournamentResponse
        .json()
        .catch(() => null);

      if (!tournamentResponse.ok || !tournamentResult) {
        startTransition(() => {
          setError(tournamentResult?.message ?? t("tournamentDetail.notFound"));
          setTournament(null);
          setLoading(false);
        });
        return;
      }

      const loadedTournament = tournamentResult as Tournament;
      const [
        gameResponse,
        formatResponse,
        finalFormatResponse,
        teamsResponse,
        matchesResponse,
      ] = await Promise.all([
        loadedTournament.game_igdb_id
          ? getGameById(apiUrl, Number(loadedTournament.game_igdb_id), t)
          : Promise.resolve({ data: null, error: null }),
        loadedTournament.format
          ? supabase
              .from("formats")
              .select("name")
              .eq("id", loadedTournament.format)
              .limit(1)
              .maybeSingle()
          : Promise.resolve({ data: null, error: null }),
        loadedTournament.final_format
          ? supabase
              .from("formats")
              .select("name")
              .eq("id", loadedTournament.final_format)
              .limit(1)
              .maybeSingle()
          : Promise.resolve({ data: null, error: null }),
        supabase
          .from("teams")
          .select("id, name, status")
          .eq("tournament_id", loadedTournament.id)
          .eq("status", "accepted")
          .order("created_at", { ascending: true }),
        supabase
          .from("matches")
          .select(
            "id, round, match_number, team1_id, team2_id, winner_id, team1_score, team2_score",
          )
          .eq("tournament_id", loadedTournament.id)
          .order("round", { ascending: true })
          .order("match_number", { ascending: true }),
      ]);

      const acceptedTeams =
        (teamsResponse.data as Omit<TournamentTeam, "players">[] | null) ?? [];
      const teamIds = acceptedTeams.map((team) => team.id);
      const { data: teamMembersData } =
        teamIds.length > 0
          ? await supabase
              .from("team_members")
              .select("id, name, team_id, created_at")
              .in("team_id", teamIds)
              .order("created_at", { ascending: true })
          : { data: [] as TournamentTeamMember[] };

      const playersByTeamId = new Map<number, TournamentTeamMember[]>();
      ((teamMembersData as TournamentTeamMember[] | null) ?? []).forEach(
        (member) => {
          const currentPlayers = playersByTeamId.get(member.team_id) ?? [];
          currentPlayers.push(member);
          playersByTeamId.set(member.team_id, currentPlayers);
        },
      );

      startTransition(() => {
        setTournament(loadedTournament);
        setError(null);
        setGameName(gameResponse.data?.name ?? "-");
        setGameCoverUrl(gameResponse.data?.coverUrl ?? null);
        setFormatName(
          formatResponse.data?.name ?? getRelationName(loadedTournament.format),
        );
        setFinalFormatName(
          finalFormatResponse.data?.name ??
            getRelationName(loadedTournament.final_format),
        );
        setTeams(
          acceptedTeams.map((team) => ({
            ...team,
            players: playersByTeamId.get(team.id) ?? [],
          })),
        );
        setMatches((matchesResponse.data as TournamentMatch[] | null) ?? []);
        setLoading(false);
      });

      const {
        data: { session },
      } = await supabase.auth.getSession();

      if (session?.user?.id) {
        const betStateResult = await loadTournamentBetState(
          apiUrl,
          Number(loadedTournament.id),
          t,
        );

        startTransition(() => {
          setCurrentBet(betStateResult.data?.bet ?? null);
          setCoinsBalance(
            typeof betStateResult.data?.coins_balance === "number"
              ? betStateResult.data.coins_balance
              : null,
          );
          setBetStateError(betStateResult.error);
        });
      } else {
        startTransition(() => {
          setCurrentBet(null);
          setCoinsBalance(null);
          setBetStateError(null);
        });
      }
    };

    void loadTournament();
  }, [apiUrl, reloadKey, slug, t]);

  useEffect(() => {
    const loadSession = async () => {
      const {
        data: { session },
      } = await supabase.auth.getSession();

      setUserId(session?.user?.id ?? null);
    };

    void loadSession();
  }, []);

  if (loading) {
    return (
      <div className="tournament-detail">
        <p>{t("tournamentDetail.messages.loading")}</p>
      </div>
    );
  }

  if (error || !tournament) {
    return (
      <div className="tournament-detail">
        <p>{error ?? t("tournamentDetail.notFound")}</p>
        <Link className="btn btn--outline btn--sm" to="/tournaments">
          {t("tournamentDetail.actions.back")}
        </Link>
      </div>
    );
  }

  const spotsLeft = Math.max(
    tournament.max_teams - tournament.current_teams,
    0,
  );
  const tournamentStatus = getTournamentStatus(tournament);
  const isUpcoming = tournamentStatus === "aankomend";
  const isLoggedIn = Boolean(userId);
  const isFull = tournament.current_teams >= tournament.max_teams;
  const isOrganizer = userId === tournament.user_id;
  const teamNameById = new Map(teams.map((team) => [team.id, team.name]));
  const hasPlacedBet = Boolean(currentBet);
  const bettingClosed = tournament.status !== "aankomend";

  const renderBetAction = (team: TournamentTeam) => {
    if (hasPlacedBet && currentBet?.team_id === team.id) {
      return (
        <span className="tournament-bet-state tournament-bet-state--active">
          {t("tournamentBetting.card.stakePlaced", {
            coins: currentBet.coins_bet,
          })}
        </span>
      );
    }

    if (bettingClosed) {
      return (
        <span className="tournament-bet-state">
          {t("tournamentBetting.card.closed")}
        </span>
      );
    }

    if (!isLoggedIn) {
      return (
        <Link to="/login" className="btn btn--outline btn--sm">
          {t("tournamentBetting.card.login")}
        </Link>
      );
    }

    if (hasPlacedBet) {
      return (
        <span className="tournament-bet-state">
          {t("tournamentBetting.card.alreadyBet")}
        </span>
      );
    }

    return (
      <button
        type="button"
        className="btn btn--outline btn--sm"
        onClick={() => {
          setBetSuccessMessage(null);
          setBetModalTeam(team);
        }}
      >
        {t("tournamentBetting.card.bet")}
      </button>
    );
  };

  return (
    <div className="tournament-detail">
      {joinPopupOpen && userId && apiUrl && (
        <TournamentParticipate
          apiUrl={apiUrl}
          isOpen={joinPopupOpen}
          tournamentId={Number(tournament.id)}
          playersPerTeam={tournament.players_per_team}
          captainId={userId}
          onClose={() => setJoinPopupOpen(false)}
          onSuccess={() => {
            setJoinSuccessMessage(t("teamRequestModal.messages.success"));
            setReloadKey((previous) => previous + 1);
          }}
        />
      )}
      {betModalTeam && apiUrl && tournament && (
        <TournamentBetModal
          apiUrl={apiUrl}
          isOpen={Boolean(betModalTeam)}
          tournamentId={Number(tournament.id)}
          teamId={betModalTeam.id}
          teamName={betModalTeam.name}
          availableCoins={coinsBalance}
          onClose={() => setBetModalTeam(null)}
          onSuccess={(remainingCoins) => {
            setBetSuccessMessage(t("tournamentBetting.messages.success"));
            if (typeof remainingCoins === "number") {
              setCoinsBalance(remainingCoins);
            }
            setReloadKey((previous) => previous + 1);
          }}
        />
      )}

      <div className="tournament-header">
        <div className="tournament-info">
          <div className="tournament-title">
            <div className="tournament-logo">
              <img
                src={gameCoverUrl ?? "/logo.webp"}
                alt={gameName !== "-" ? gameName : t("common.logoAlt")}
                width="88"
                height="120"
              />
            </div>
            <div className="tournament-title-text">
              <div className="tournament-title-badges">
                <p className="badge badge-detail">{tournamentStatus}</p>
                <p
                  className={`badge badge-detail badge-detail--privacy badge-detail--${tournament.privacy}`}
                >
                  {getPrivacyLabel(tournament.privacy, t)}
                </p>
              </div>
              <h2>{tournament.name}</h2>
            </div>
          </div>

          <div>
            <div className="tournament-meta">
              <div className="tournament-meta-content">
                <span className="tournament-meta-line">
                  <ControllerIcon className="tournament-meta-icon" />
                  {gameName}
                </span>
                <span className="tournament-meta-line">
                  <CalendarIcon className="tournament-meta-icon" />
                  {formatDateYear(tournament.start_date, tournament.end_date)}
                </span>
              </div>
              <div className="tournament-meta-content">
                <span className="tournament-meta-line">
                  <UsersIcon className="tournament-meta-icon" />
                  {t("tournamentDetail.metaTeams", {
                    current: tournament.current_teams,
                    max: tournament.max_teams,
                  })}
                </span>
                <span className="tournament-meta-line">
                  <ClockIcon className="tournament-meta-icon" />
                  {formatHours(tournament.start_date)}
                </span>
              </div>
            </div>

            {joinSuccessMessage && (
              <p className="tournament-detail__success">{joinSuccessMessage}</p>
            )}
            {betSuccessMessage && (
              <p className="tournament-detail__success">{betSuccessMessage}</p>
            )}
            {betStateError && (
              <p className="team-request-modal__error">{betStateError}</p>
            )}

            <div className="btn-group">
              {isOrganizer && (
                <Link
                  to={`/tournament/${slug}/admin`}
                  className="btn btn--outline"
                >
                  {t("tournamentDetail.actions.admin")}
                </Link>
              )}
              {isUpcoming &&
                !isFull &&
                (isLoggedIn ? (
                  <button
                    type="button"
                    className="btn btn--primary"
                    onClick={() => {
                      setJoinSuccessMessage(null);
                      setJoinPopupOpen(true);
                    }}
                  >
                    {t("tournamentDetail.join")}
                  </button>
                ) : (
                  <Link to="/login" className="btn btn--primary">
                    {t("tournamentDetail.loginToJoin")}
                  </Link>
                ))}
            </div>
          </div>
        </div>

        <div className="tournament-status-card">
          <h3>{t("tournamentDetail.status.title")}</h3>

          <p>
            <strong>{t("tournamentDetail.rules.maxTeams")}</strong>{" "}
            {tournament.max_teams}
          </p>
          <p>
            <strong>{t("tournamentDetail.status.playersPerTeam")}</strong>{" "}
            {tournament.players_per_team}
          </p>
          <div className="progress-section">
            <p>
              {t("tournamentDetail.status.progress")} {spotsLeft}
            </p>
            <div className="progress-bar">
              <div
                className="progress-fill"
                style={{
                  width: `${(tournament.current_teams / tournament.max_teams) * 100}%`,
                }}
              />
            </div>
          </div>
        </div>
      </div>
      <div className="tournament-section" id="overview">
        <div className="tournament-description-card">
          <h3>{t("tournamentDetail.descriptionTitle")}</h3>
          <p>
            {tournament.description?.trim() ||
              t("tournamentDetail.descriptionEmpty")}
          </p>
        </div>
      </div>

      <div
        className={`tournament-tabs ${
          activeTab === "bracket" ? "tournament-tabs--flush" : ""
        }`}
      >
        <Link to="#bracket" className={activeTab === "bracket" ? "active" : ""}>
          {t("tournamentDetail.tabs.bracket")}
        </Link>
        <Link to="#members" className={activeTab === "members" ? "active" : ""}>
          {t("tournamentDetail.tabs.members")} ({tournament.current_teams})
        </Link>

        <Link to="#rules" className={activeTab === "rules" ? "active" : ""}>
          {t("tournamentDetail.tabs.rules")}
        </Link>
      </div>
      {activeTab === "bracket" && (
        <div className="tournament-section tournament-section--bracket" id="bracket">
          <TournamentBracket
            matches={matches}
            teamNameById={teamNameById}
            emptyLabel={t("tournamentDetail.placeholder")}
            placeholderLabel={t("tournamentDetail.bracketTbd")}
          />
        </div>
      )}

      {activeTab === "members" && (
        <div className="tournament-section" id="members">
          <h2>{t("tournamentDetail.tabs.members")}</h2>
          {teams.length > 0 ? (
            <div className="tournament-members-grid">
              {teams.map((team) => (
                <article className="tournament-member-card" key={team.id}>
                  <div className="tournament-member-card__header">
                    <h3>{team.name}</h3>
                    {renderBetAction(team)}
                  </div>

                  {team.players.length > 0 ? (
                    <ul className="tournament-member-card__players">
                      {team.players.map((player) => (
                        <li key={player.id}>{player.name}</li>
                      ))}
                    </ul>
                  ) : (
                    <p className="tournament-member-card__empty">
                      {t("tournamentDetail.noPlayers")}
                    </p>
                  )}
                </article>
              ))}
            </div>
          ) : (
            <div className="bracket-placeholder">
              <p>
                {t("tournamentDetail.membersPlaceholderCount", {
                  current: tournament.current_teams,
                  max: tournament.max_teams,
                })}
              </p>
            </div>
          )}
        </div>
      )}

      {activeTab === "rules" && (
        <div className="tournament-section" id="rules">
          <h2>{t("tournamentDetail.tabs.rules")}</h2>
          <ul className="rules-box">
            <li>
              {t("tournamentDetail.overview.game")} {gameName}
            </li>
            <li>
              {t("tournamentDetail.overview.format")} {formatName}
            </li>
            <li>
              {t("tournamentDetail.overview.finalFormat")} {finalFormatName}
            </li>
            <li>
              {t("tournamentDetail.overview.start")}{" "}
              {dateYear(tournament.start_date)}
            </li>
            <li>
              {t("tournamentDetail.overview.end")}{" "}
              {dateYear(tournament.end_date)}
            </li>
            <li>
              {t("tournamentDetail.overview.startTime")}{" "}
              {formatHours(tournament.start_date)}
            </li>
            <li>
              {t("tournamentDetail.rules.minTeams")} {tournament.min_teams}
            </li>
            <li>
              {t("tournamentDetail.rules.maxTeams")} {tournament.max_teams}
            </li>
            <li>
              {t("tournamentDetail.rules.playersPerTeam")}{" "}
              {tournament.players_per_team}
            </li>
          </ul>
        </div>
      )}
    </div>
  );
}
