import { startTransition, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router-dom";
import TeamRequestCard from "../../components/TeamRequestCard";
import TournamentAdminNav from "../../components/tournaments/admin/TournamentAdminNav";
import {
  acceptTeamRequest,
  createAdminTeam,
  loadTournamentAdminTeamRequests,
  rejectTeamRequest,
  type TeamRequestItem,
  type TournamentAdminSummary,
} from "../../action/teamRequestsAdmin";
import { supabase } from "../../lib/supabaseClient";

const apiUrl = import.meta.env.VITE_API_URL;

export default function TournamentAdminTeams() {
  const { t } = useTranslation();
  const { id: slug } = useParams<{ id: string }>();

  const [tournament, setTournament] = useState<TournamentAdminSummary | null>(null);
  const [teams, setTeams] = useState<TeamRequestItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [processingTeamId, setProcessingTeamId] = useState<number | null>(null);
  const [creatingTeam, setCreatingTeam] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [adminTeamName, setAdminTeamName] = useState("");
  const [organizerId, setOrganizerId] = useState<string | null>(null);
  const [adminPlayerNames, setAdminPlayerNames] = useState<string[]>([]);

  useEffect(() => {
    const loadPage = async () => {
      if (!slug) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.missingSlug"));
          setLoading(false);
        });
        return;
      }

      startTransition(() => {
        setLoading(true);
        setError(null);
        setActionError(null);
      });

      const {
        data: { session },
      } = await supabase.auth.getSession();

      if (!session?.user?.id) {
        startTransition(() => {
          setOrganizerId(null);
          setTournament(null);
          setTeams([]);
          setError(t("tournamentAdmin.messages.loginRequired"));
          setLoading(false);
        });
        return;
      }

      startTransition(() => {
        setOrganizerId(session.user.id);
      });

      const result = await loadTournamentAdminTeamRequests(slug);
      if (result.error || !result.tournament) {
        startTransition(() => {
          setTournament(null);
          setTeams([]);
          setError(result.error ?? t("tournamentAdmin.messages.loadFailed"));
          setLoading(false);
        });
        return;
      }

      if (result.tournament.user_id !== session.user.id) {
        startTransition(() => {
          setTournament(result.tournament);
          setTeams([]);
          setError(t("tournamentAdmin.messages.organizerOnly"));
          setLoading(false);
        });
        return;
      }

      const loadedTournament = result.tournament;

      startTransition(() => {
        setTournament(loadedTournament);
        setTeams(result.teams);
        setAdminPlayerNames((current) =>
          Array.from(
            { length: loadedTournament.players_per_team },
            (_, index) => current[index] ?? "",
          ),
        );
        setLoading(false);
      });
    };

    void loadPage();
  }, [slug, t]);

  const refreshTeams = async () => {
    if (!slug) {
      return;
    }

    const result = await loadTournamentAdminTeamRequests(slug);
    if (result.error || !result.tournament) {
      setError(result.error ?? t("tournamentAdmin.messages.refreshFailed"));
      return;
    }

    setTournament(result.tournament);
    setTeams(result.teams);
  };

  const handleAccept = async (teamId: number) => {
    if (!apiUrl) {
      setActionError(t("tournamentAdmin.messages.missingApiUrl"));
      return;
    }

    setProcessingTeamId(teamId);
    setActionError(null);
    setActionMessage(null);

    const result = await acceptTeamRequest(apiUrl, teamId, t);
    if (!result.ok) {
      setActionError(result.message);
      setProcessingTeamId(null);
      return;
    }

    await refreshTeams();
    setActionMessage(result.message);
    setProcessingTeamId(null);
  };

  const handleReject = async (teamId: number, rejectionReason: string) => {
    if (!apiUrl) {
      setActionError(t("tournamentAdmin.messages.missingApiUrl"));
      return;
    }

    setProcessingTeamId(teamId);
    setActionError(null);
    setActionMessage(null);

    const result = await rejectTeamRequest(apiUrl, teamId, rejectionReason, t);
    if (!result.ok) {
      setActionError(result.message);
      setProcessingTeamId(null);
      return;
    }

    await refreshTeams();
    setActionMessage(result.message);
    setProcessingTeamId(null);
  };

  const handleAdminPlayerNameChange = (index: number, value: string) => {
    setAdminPlayerNames((current) =>
      current.map((playerName, playerIndex) =>
        playerIndex === index ? value : playerName,
      ),
    );
  };

  const handleCreateAdminTeam = async () => {
    if (!apiUrl) {
      setActionError(t("tournamentAdmin.messages.missingApiUrl"));
      return;
    }

    if (!tournament) {
      setActionError(t("tournamentAdmin.messages.tournamentNotFound"));
      return;
    }

    if (!adminTeamName.trim()) {
      setActionError(t("tournamentAdmin.messages.teamNameRequired"));
      return;
    }

    if (!organizerId) {
      setActionError(t("tournamentAdmin.messages.captainIdRequired"));
      return;
    }

    if (adminPlayerNames.some((playerName) => !playerName.trim())) {
      setActionError(t("tournamentAdmin.messages.playerNamesRequired"));
      return;
    }

    setCreatingTeam(true);
    setActionError(null);
    setActionMessage(null);

    const result = await createAdminTeam(apiUrl, {
      tournamentId: tournament.id,
      captainId: organizerId,
      teamName: adminTeamName.trim(),
      playerNames: adminPlayerNames.map((playerName) => playerName.trim()),
    }, t);

    setCreatingTeam(false);

    if (!result.ok) {
      setActionError(result.message);
      return;
    }

    setAdminTeamName("");
    setAdminPlayerNames(Array.from({ length: tournament.players_per_team }, () => ""));
    setCreateModalOpen(false);
    await refreshTeams();
    setActionMessage(result.message);
  };

  const pendingTeams = teams.filter((team) => team.status === "pending");
  const acceptedTeams = teams.filter((team) => team.status === "accepted");
  const rejectedTeams = teams.filter((team) => team.status === "rejected");

  if (loading) {
    return (
      <div className="team-requests-admin">
        <p>{t("tournamentAdmin.loading")}</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="team-requests-admin">
        <p>{error}</p>
        <Link className="btn btn--outline btn--sm" to={`/tournament/${slug}`}>
          {t("tournamentAdmin.actions.backToTournament")}
        </Link>
      </div>
    );
  }

  return (
    <div className="team-requests-admin">
      <header className="team-requests-admin__header">
        <div>
          <p className="team-requests-admin__eyebrow">{t("tournamentAdmin.eyebrow")}</p>
          <h1>{tournament?.name}</h1>
          <p>{t("tournamentAdmin.subtitle")}</p>
        </div>

        <Link className="btn btn--outline" to={`/tournament/${slug}`}>
          {t("tournamentAdmin.actions.backToTournament")}
        </Link>
      </header>

      {slug && <TournamentAdminNav slug={slug} active="teams" />}

      {actionError && <p className="team-requests-admin__feedback team-requests-admin__feedback--error">{actionError}</p>}
      {actionMessage && <p className="team-requests-admin__feedback team-requests-admin__feedback--success">{actionMessage}</p>}

      <section className="team-requests-admin__toolbar">
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => {
            setActionError(null);
            setCreateModalOpen(true);
          }}
        >
          {t("tournamentAdmin.actions.addAcceptedTeam")}
        </button>
      </section>

      {createModalOpen && (
        <div className="team-requests-admin__modal">
          <div
            className="team-requests-admin__modal-backdrop"
            onClick={() => !creatingTeam && setCreateModalOpen(false)}
          />
          <div className="team-requests-admin__modal-panel">
            <div className="team-requests-admin__section-header">
              <h2>{t("tournamentAdmin.create.title")}</h2>
              <button
                type="button"
                className="btn btn--outline btn--sm"
                onClick={() => setCreateModalOpen(false)}
                disabled={creatingTeam}
              >
                {t("teamRequestModal.actions.cancel")}
              </button>
            </div>

            <div className="team-requests-admin__create-grid">
              <label className="form-group">
                <span>{t("tournamentAdmin.create.teamName")}</span>
                <input
                  type="text"
                  value={adminTeamName}
                  onChange={(event) => setAdminTeamName(event.target.value)}
                  disabled={creatingTeam}
                />
              </label>
            </div>

            <div className="team-requests-admin__create-grid">
              {adminPlayerNames.map((playerName, index) => (
                <label className="form-group" key={index}>
                  <span>{t("tournamentAdmin.create.playerLabel", { index: index + 1 })}</span>
                  <input
                    type="text"
                    value={playerName}
                    onChange={(event) =>
                      handleAdminPlayerNameChange(index, event.target.value)
                    }
                    disabled={creatingTeam}
                  />
                </label>
              ))}
            </div>

            <div className="team-requests-admin__create-actions">
              <button
                type="button"
                className="btn btn--primary"
                onClick={handleCreateAdminTeam}
                disabled={creatingTeam}
              >
                {creatingTeam
                  ? t("tournamentAdmin.actions.creating")
                  : t("tournamentAdmin.actions.addAcceptedTeam")}
              </button>
            </div>
          </div>
        </div>
      )}

      <section className="team-requests-admin__section">
        <div className="team-requests-admin__section-header">
          <h2>{t("tournamentAdmin.sections.pending")}</h2>
          <span>{pendingTeams.length}</span>
        </div>
        <div className="team-requests-admin__grid">
          {pendingTeams.length > 0 ? (
            pendingTeams.map((team) => (
              <TeamRequestCard
                key={team.id}
                team={team}
                isProcessing={processingTeamId === team.id}
                onAccept={handleAccept}
                onReject={handleReject}
              />
            ))
          ) : (
            <p className="team-requests-admin__empty">{t("tournamentAdmin.empty.pending")}</p>
          )}
        </div>
      </section>

      <section className="team-requests-admin__section">
        <div className="team-requests-admin__section-header">
          <h2>{t("tournamentAdmin.sections.accepted")}</h2>
          <span>{acceptedTeams.length}</span>
        </div>
        <div className="team-requests-admin__grid">
          {acceptedTeams.length > 0 ? (
            acceptedTeams.map((team) => (
              <TeamRequestCard
                key={team.id}
                team={team}
                isProcessing={processingTeamId === team.id}
                onAccept={handleAccept}
                onReject={handleReject}
              />
            ))
          ) : (
            <p className="team-requests-admin__empty">{t("tournamentAdmin.empty.accepted")}</p>
          )}
        </div>
      </section>

      <section className="team-requests-admin__section">
        <div className="team-requests-admin__section-header">
          <h2>{t("tournamentAdmin.sections.rejected")}</h2>
          <span>{rejectedTeams.length}</span>
        </div>
        <div className="team-requests-admin__grid">
          {rejectedTeams.length > 0 ? (
            rejectedTeams.map((team) => (
              <TeamRequestCard
                key={team.id}
                team={team}
                isProcessing={processingTeamId === team.id}
                onAccept={handleAccept}
                onReject={handleReject}
              />
            ))
          ) : (
            <p className="team-requests-admin__empty">{t("tournamentAdmin.empty.rejected")}</p>
          )}
        </div>
      </section>
    </div>
  );
}
