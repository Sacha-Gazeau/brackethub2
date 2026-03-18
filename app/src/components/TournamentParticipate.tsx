import { useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import {
  submitTeamRequest,
  validateTeamRequestForm,
} from "../action/teamRequestJoin";

type TournamentParticipateProps = {
  apiUrl: string;
  isOpen: boolean;
  tournamentId: number;
  playersPerTeam: number;
  captainId: string;
  onClose: () => void;
  onSuccess: () => void;
};

function createInitialPlayerNames(playersPerTeam: number) {
  return Array.from({ length: playersPerTeam }, () => "");
}

export default function TournamentParticipate({
  apiUrl,
  isOpen,
  tournamentId,
  playersPerTeam,
  captainId,
  onClose,
  onSuccess,
}: TournamentParticipateProps) {
  const { t } = useTranslation();
  const [teamName, setTeamName] = useState("");
  const [playerNames, setPlayerNames] = useState<string[]>(
    createInitialPlayerNames(playersPerTeam),
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!isOpen) {
    return null;
  }

  const setPlayerName = (index: number, value: string) => {
    setPlayerNames((current) =>
      current.map((playerName, playerIndex) =>
        playerIndex === index ? value : playerName,
      ),
    );
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);

    const validationError = validateTeamRequestForm(teamName, playerNames, t);
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    setIsSubmitting(true);

    const result = await submitTeamRequest(apiUrl, {
      tournamentId,
      captainId,
      teamName: teamName.trim(),
      playerNames: playerNames.map((playerName) => playerName.trim()),
    }, t);

    setIsSubmitting(false);

    if (!result.ok) {
      setErrorMessage(result.message);
      return;
    }

    onSuccess();
    onClose();
  };

  return (
    <div className="team-request-modal" role="dialog" aria-modal="true">
      <div className="team-request-modal__backdrop" onClick={onClose} />

      <div className="team-request-modal__panel">
        <div className="team-request-modal__header">
          <div>
            <p className="team-request-modal__eyebrow">
              {t("teamRequestModal.eyebrow")}
            </p>
            <h3>{t("teamRequestModal.title")}</h3>
          </div>

          <button
            type="button"
            className="btn btn--outline btn--sm"
            onClick={onClose}
            disabled={isSubmitting}
          >
            {t("teamRequestModal.actions.cancel")}
          </button>
        </div>

        <form className="team-request-modal__form" onSubmit={handleSubmit}>
          {errorMessage && (
            <p className="team-request-modal__error">{errorMessage}</p>
          )}

          <div className="form-group">
            <label htmlFor="team-name">{t("teamRequestModal.fields.teamName")}</label>
            <input
              id="team-name"
              type="text"
              placeholder={t("teamRequestModal.fields.teamNamePlaceholder")}
              value={teamName}
              onChange={(event) => setTeamName(event.target.value)}
              disabled={isSubmitting}
            />
          </div>

          <div className="team-request-modal__players">
            <h4>{t("teamRequestModal.fields.playerNames")}</h4>
            <div className="team-request-modal__players-grid">
              {playerNames.map((playerName, index) => (
                <div className="form-group" key={index}>
                  <label htmlFor={`player-name-${index}`}>
                    {t("teamRequestModal.fields.playerLabel", {
                      index: index + 1,
                    })}
                  </label>
                  <input
                    id={`player-name-${index}`}
                    type="text"
                    placeholder={t("teamRequestModal.fields.playerPlaceholder", {
                      index: index + 1,
                    })}
                    value={playerName}
                    onChange={(event) => setPlayerName(index, event.target.value)}
                    disabled={isSubmitting}
                  />
                </div>
              ))}
            </div>
          </div>

          <div className="team-request-modal__actions">
            <button
              type="button"
              className="btn btn--outline"
              onClick={onClose}
              disabled={isSubmitting}
            >
              {t("teamRequestModal.actions.cancel")}
            </button>
            <button type="submit" className="btn btn--primary" disabled={isSubmitting}>
              {isSubmitting
                ? t("teamRequestModal.actions.submitting")
                : t("teamRequestModal.actions.submit")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
