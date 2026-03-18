import { startTransition, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { supabase } from "../../lib/supabaseClient";
import TournamentAdminNav from "../../components/tournaments/admin/TournamentAdminNav";
import {
  canLaunchTournamentStage,
  loadTournamentAdminStageData,
  type TournamentAdminStageSummary,
} from "../../action/tournamentAdminStage";

export default function TournamentAdminDashboard() {
  const { t } = useTranslation();
  const { id: slug } = useParams<{ id: string }>();
  const [tournament, setTournament] = useState<TournamentAdminStageSummary | null>(null);
  const [acceptedTeamsCount, setAcceptedTeamsCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadPage = async () => {
      if (!slug) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.missingSlug"));
          setLoading(false);
        });
        return;
      }

      const {
        data: { session },
      } = await supabase.auth.getSession();

      if (!session?.user?.id) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.loginRequired"));
          setLoading(false);
        });
        return;
      }

      const result = await loadTournamentAdminStageData(slug, t);
      if (result.error || !result.tournament) {
        startTransition(() => {
          setError(result.error ?? t("tournamentAdmin.messages.loadFailed"));
          setLoading(false);
        });
        return;
      }

      if (result.tournament.user_id !== session.user.id) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.organizerOnly"));
          setLoading(false);
        });
        return;
      }

      startTransition(() => {
        setTournament(result.tournament);
        setAcceptedTeamsCount(result.acceptedTeams.length);
        setLoading(false);
      });
    };

    void loadPage();
  }, [slug, t]);

  if (loading) {
    return <div className="tournament-admin-page"><p>{t("tournamentAdmin.loading")}</p></div>;
  }

  if (error || !tournament || !slug) {
    return (
      <div className="tournament-admin-page">
        <p>{error ?? t("tournamentAdmin.messages.tournamentNotFound")}</p>
      </div>
    );
  }

  const launchState = canLaunchTournamentStage(tournament, acceptedTeamsCount);

  return (
    <div className="tournament-admin-page">
      <header className="tournament-admin-page__header">
        <div>
          <p className="tournament-admin-page__eyebrow">Toernooi beheer</p>
          <h1>{tournament.name}</h1>
          <p>Beheer hier de teams en de toernooistructuur.</p>
        </div>

        <Link className="btn btn--outline" to={`/tournament/${slug}`}>
          {t("tournamentAdmin.actions.backToTournament")}
        </Link>
      </header>

      <TournamentAdminNav slug={slug} active="dashboard" />

      <section className="tournament-admin-dashboard">
        <Link className="tournament-admin-dashboard__card" to={`/tournament/${slug}/admin/teams`}>
          <span>Teams</span>
          <strong>{acceptedTeamsCount} geaccepteerd</strong>
          <p>Bekijk aanvragen, accepteer teams en beheer de deelnemerslijst.</p>
        </Link>

        <Link className="tournament-admin-dashboard__card" to={`/tournament/${slug}/admin/stage`}>
          <span>Structuur</span>
          <strong>{tournament.tournament_type}</strong>
          <p>
            {launchState.structureExists
              ? "Er bestaat al een toernooistructuur."
              : launchState.canLaunch
                ? "Klaar om de toernooistructuur te starten."
                : "Wacht op het minimum aantal geaccepteerde teams."}
          </p>
        </Link>
      </section>
    </div>
  );
}
