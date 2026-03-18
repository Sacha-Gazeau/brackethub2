import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { formatDate } from "../action/date";
import { memo, useEffect, useState } from "react";
import type { TournamentItem } from "../action/tournamentFilter";
import { getTournamentStatus } from "../action/tournamentStatus";
import { supabase } from "../lib/supabaseClient";

type TournamentCardLogoutProps = {
  tournaments?: TournamentItem[];
  emptyState?: {
    title: string;
    description: string;
    actionLabel: string;
    actionTo: string;
  };
};

function TournamentCardlogoutComponent({
  tournaments,
  emptyState,
}: TournamentCardLogoutProps) {
  const { t } = useTranslation();
  const isLogin = true;
  const tournamentsToRender = tournaments ?? [];

  return (
    <div className="cards">
      {tournamentsToRender.length === 0 && emptyState ? (
        <div className="card card--empty">
          <div className="card-top">
            <span className="badge badge-aankomend">{emptyState.title}</span>
          </div>
          <h3>{emptyState.title}</h3>
          <p className="date">{emptyState.description}</p>
          <div className="card-actions">
            <Link className="btn btn--primary btn--sm" to={emptyState.actionTo}>
              {emptyState.actionLabel}
            </Link>
          </div>
        </div>
      ) : null}

      {tournamentsToRender.map((d) => (
        (() => {
          const isOfficialTournament = d.privacy === "official";

          return (
            <div key={d.slug ?? d.id} className="card">
              <div className="card-top">
                <span className={`badge badge-${getTournamentStatus(d)}`}>
                  {getTournamentStatus(d)}
                </span>

                <span className="format">
                  {d.players_per_team}v{d.players_per_team}
                </span>
              </div>
              <h3>{d.name}</h3>
              <p className="date">{formatDate(d.start_date, d.end_date)}</p>
              <p className="teams">
                {t("tournamentCard.teamsCount", {
                  current: d.current_teams,
                  max: d.max_teams,
                })}
              </p>
              {!isLogin ? (
                <div className="card-actions">
                  <Link
                    className="btn btn--outline btn--sm"
                    to={`/tournament/${d.slug}`}
                  >
                    {t("tournamentCard.view")}
                  </Link>
                </div>
              ) : (
                <div className="card-actions">
                  <Link
                    className="btn btn--outline btn--sm"
                    to={`/tournament/${d.slug}`}
                  >
                    {t("tournamentCard.view")}
                  </Link>
                  {isOfficialTournament && (
                    <Link
                      className="btn btn--primary btn--sm"
                      to={`/tournament/${d.slug}#bracket`}
                    >
                      {t("tournamentCard.bet")}
                    </Link>
                  )}
                </div>
              )}
            </div>
          );
        })()
      ))}
    </div>
  );
}

export const TournamentCardlogout = memo(TournamentCardlogoutComponent);
export function TournamentCardUpcoming() {
  const { t } = useTranslation();
  const isLogin = false;
  const [tournaments, setTournaments] = useState<TournamentItem[]>([]);

  useEffect(() => {
    const loadTournaments = async () => {
      const { data, error } = await supabase
        .from("tournaments")
        .select("*");
      if (error) {
        console.error("Tournament LOAD ERROR:", error);
        return;
      }

      setTournaments(
        ((data ?? []) as TournamentItem[]).filter(
          (tournament) => getTournamentStatus(tournament) === "aankomend",
        ),
      );
    };

    loadTournaments();
  }, []);
  return (
    <div className="cards">
      {tournaments.map((d) => (
        (() => {
          const isOfficialTournament = d.privacy === "official";

          return (
            <div key={d.slug ?? d.id} className="card">
              <div className="card-top">
                <span className={`badge badge-${getTournamentStatus(d)}`}>
                  {getTournamentStatus(d)}
                </span>

                <span className="format">
                  {d.players_per_team}v{d.players_per_team}
                </span>
              </div>
              <h3>{d.name}</h3>
              <p className="date">{formatDate(d.start_date, d.end_date)}</p>
              <p className="teams">
                {t("tournamentCard.teamsCount", {
                  current: d.current_teams,
                  max: d.max_teams,
                })}
              </p>
              {isLogin ? (
                <div className="card-actions">
                  <Link
                    className="btn btn--outline btn--sm"
                    to={`/tournament/${d.slug}`}
                  >
                    {t("tournamentCard.view")}
                  </Link>
                </div>
              ) : (
                <div className="card-actions">
                  <Link
                    className="btn btn--outline btn--sm"
                    to={`/tournament/${d.slug}`}
                  >
                    {t("tournamentCard.view")}
                  </Link>
                  {isOfficialTournament && (
                    <Link
                      className="btn btn--primary btn--sm"
                      to={`/tournament/${d.slug}#wedden`}
                    >
                      {t("tournamentCard.bet")}
                    </Link>
                  )}
                </div>
              )}
            </div>
          );
        })()
      ))}
    </div>
  );
}
