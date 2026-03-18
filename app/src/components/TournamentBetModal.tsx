import { useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import {
  placeTournamentBet,
  validateTournamentBetAmount,
} from "../action/tournamentBetting";

type TournamentBetModalProps = {
  apiUrl: string;
  isOpen: boolean;
  tournamentId: number;
  teamId: number;
  teamName: string;
  availableCoins: number | null;
  onClose: () => void;
  onSuccess: (remainingCoins?: number) => void;
};

export default function TournamentBetModal({
  apiUrl,
  isOpen,
  tournamentId,
  teamId,
  teamName,
  availableCoins,
  onClose,
  onSuccess,
}: TournamentBetModalProps) {
  const { t } = useTranslation();
  const [coinsBet, setCoinsBet] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);

    const validationError = validateTournamentBetAmount(coinsBet, availableCoins, t);
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    setIsSubmitting(true);

    const result = await placeTournamentBet(
      apiUrl,
      {
        tournamentId,
        teamId,
        coinsBet: Number(coinsBet),
      },
      t,
    );

    setIsSubmitting(false);

    if (!result.ok) {
      setErrorMessage(result.message);
      return;
    }

    onSuccess(result.remainingCoins);
    setCoinsBet("");
    onClose();
  };

  return (
    <div className="team-request-modal" role="dialog" aria-modal="true">
      <div className="team-request-modal__backdrop" onClick={onClose} />

      <div className="team-request-modal__panel tournament-bet-modal">
        <div className="team-request-modal__header">
          <div>
            <p className="team-request-modal__eyebrow">
              {t("tournamentBetting.modal.eyebrow")}
            </p>
            <h3>{t("tournamentBetting.modal.title", { team: teamName })}</h3>
            <p className="tournament-bet-modal__balance">
              {t("tournamentBetting.modal.balance", {
                coins: availableCoins ?? 0,
              })}
            </p>
          </div>

          <button
            type="button"
            className="btn btn--outline btn--sm"
            onClick={onClose}
            disabled={isSubmitting}
          >
            {t("tournamentBetting.actions.cancel")}
          </button>
        </div>

        <form className="team-request-modal__form" onSubmit={handleSubmit}>
          {errorMessage && (
            <p className="team-request-modal__error">{errorMessage}</p>
          )}

          <div className="form-group">
            <label htmlFor="bet-amount">{t("tournamentBetting.fields.amount")}</label>
            <input
              id="bet-amount"
              type="number"
              min="1"
              step="1"
              value={coinsBet}
              onChange={(event) => setCoinsBet(event.target.value)}
              placeholder={t("tournamentBetting.fields.amountPlaceholder")}
              disabled={isSubmitting}
            />
          </div>

          <p className="tournament-bet-modal__hint">
            {t("tournamentBetting.modal.hint")}
          </p>

          <div className="team-request-modal__actions">
            <button
              type="button"
              className="btn btn--outline"
              onClick={onClose}
              disabled={isSubmitting}
            >
              {t("tournamentBetting.actions.cancel")}
            </button>
            <button type="submit" className="btn btn--primary" disabled={isSubmitting}>
              {isSubmitting
                ? t("tournamentBetting.actions.submitting")
                : t("tournamentBetting.actions.confirm")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
