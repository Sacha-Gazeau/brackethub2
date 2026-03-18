import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { TeamRequestItem } from "../action/teamRequestsAdmin";

type TeamRequestCardProps = {
  team: TeamRequestItem;
  isProcessing: boolean;
  onAccept: (teamId: number) => Promise<void>;
  onReject: (teamId: number, rejectionReason: string) => Promise<void>;
};

function formatCaptain(team: TeamRequestItem) {
  if (team.captain?.username?.trim()) {
    return team.captain.username;
  }

  return "Onbekende gebruiker";
}

export default function TeamRequestCard({
  team,
  isProcessing,
  onAccept,
  onReject,
}: TeamRequestCardProps) {
  const { t } = useTranslation();
  const [isRejecting, setIsRejecting] = useState(false);
  const [rejectionReason, setRejectionReason] = useState(team.rejection_reason ?? "");
  const [localError, setLocalError] = useState<string | null>(null);

  const canAccept = team.status === "pending" || team.status === "rejected";
  const canReject = team.status === "pending" || team.status === "accepted";

  const handleAccept = async () => {
    setLocalError(null);
    setIsRejecting(false);
    await onAccept(team.id);
  };

  const handleReject = async () => {
    const trimmedReason = rejectionReason.trim();

    if (!trimmedReason) {
      setLocalError(t("tournamentAdmin.card.rejectionReasonRequired"));
      return;
    }

    setLocalError(null);
    await onReject(team.id, trimmedReason);
    setIsRejecting(false);
  };

  return (
    <article className={`team-request-card team-request-card--${team.status}`}>
      <div className="team-request-card__header">
        <div>
          <p className="team-request-card__status">{team.status}</p>
          <h3>{team.name}</h3>
        </div>

        <div className="team-request-card__actions">
          {canAccept && (
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleAccept}
              disabled={isProcessing}
            >
              {t("tournamentAdmin.card.accept")}
            </button>
          )}

          {canReject && (
            <button
              type="button"
              className="btn btn--outline"
              onClick={() => {
                setLocalError(null);
                setIsRejecting((current) => !current);
              }}
              disabled={isProcessing}
            >
              {t("tournamentAdmin.card.reject")}
            </button>
          )}
        </div>
      </div>

      <dl className="team-request-card__meta">
        <div>
          <dt>{t("tournamentAdmin.card.captain")}</dt>
          <dd>{formatCaptain(team)}</dd>
        </div>
      </dl>

      <div className="team-request-card__players">
        <h4>{t("tournamentAdmin.card.players")}</h4>
        {team.players.length > 0 ? (
          <ul>
            {team.players.map((player) => (
              <li key={player.id}>{player.name}</li>
            ))}
          </ul>
        ) : (
          <p>{t("tournamentAdmin.card.noPlayers")}</p>
        )}
      </div>

      {team.rejection_reason && !isRejecting && (
        <div className="team-request-card__reason">
          <h4>{t("tournamentAdmin.card.rejectionReason")}</h4>
          <p>{team.rejection_reason}</p>
        </div>
      )}

      {isRejecting && (
        <div className="team-request-card__reject-box">
          <label htmlFor={`reject-reason-${team.id}`}>
            {t("tournamentAdmin.card.rejectionReason")}
          </label>
          <textarea
            id={`reject-reason-${team.id}`}
            value={rejectionReason}
            onChange={(event) => setRejectionReason(event.target.value)}
            placeholder={t("tournamentAdmin.card.rejectionPlaceholder")}
            rows={4}
            disabled={isProcessing}
          />
          {localError && <p className="team-request-card__error">{localError}</p>}
          <div className="team-request-card__reject-actions">
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleReject}
              disabled={isProcessing}
            >
              {t("tournamentAdmin.card.confirmReject")}
            </button>
          </div>
        </div>
      )}
    </article>
  );
}
