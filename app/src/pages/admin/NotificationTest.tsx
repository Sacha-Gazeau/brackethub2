import { useState } from "react";
import { usePageSeo } from "../../action/usePageSeo";
import {
  sendNotificationTest,
  type NotificationTestType,
} from "../../action/notificationTest";

type TestAction = {
  type: NotificationTestType;
  title: string;
  description: string;
};

const testActions: TestAction[] = [
  {
    type: "welcome",
    title: "Welcome DM",
    description: "Simule le message de bienvenue après création de compte.",
  },
  {
    type: "join_request",
    title: "Join Request Organiser",
    description: "Simule une demande de participation reçue par l'organisateur.",
  },
  {
    type: "join_accept",
    title: "Join Accepted",
    description: "Simule un DM d'acceptation de demande d'équipe.",
  },
  {
    type: "join_reject",
    title: "Join Rejected",
    description: "Simule un DM de refus avec raison.",
  },
  {
    type: "reminder",
    title: "Tournament Reminder",
    description: "Simule le rappel envoyé 1 heure avant le tournoi.",
  },
  {
    type: "bet_won",
    title: "Bet Won",
    description: "Simule un DM de pari gagné.",
  },
  {
    type: "bet_lost",
    title: "Bet Lost",
    description: "Simule un DM de pari perdu.",
  },
  {
    type: "reward",
    title: "Reward Delivery",
    description: "Simule un DM de reward livrée.",
  },
];

export default function NotificationTestPage() {
  const apiUrl = import.meta.env.VITE_API_URL;
  const [submittingType, setSubmittingType] = useState<NotificationTestType | null>(
    null,
  );
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  usePageSeo({
    title: "BracketHub | Test Notifications",
    description: "Panneau de test pour simuler les DM Discord backend de BracketHub.",
  });

  const handleSend = async (type: NotificationTestType) => {
    setSubmittingType(type);
    setFeedback(null);
    setError(null);

    const result = await sendNotificationTest(apiUrl, type, (key) => key);
    if (!result.ok) {
      setError(result.message);
      setSubmittingType(null);
      return;
    }

    setFeedback(result.message);
    setSubmittingType(null);
  };

  return (
    <div className="notification-test-page">
      <section className="page-block">
        <div className="page-shell page-header__inner">
          <div className="page-header__content">
            <div>
              <h1 className="page-header__title">Discord DM Test</h1>
              <p className="page-header__subtitle">
                Déclenche des simulations backend pour recevoir les messages en DM
                Discord, sans recréer de compte ni de tournoi.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="section-block">
        <div className="page-shell notification-test-page__inner">
          {(feedback || error) && (
            <div
              className={`notification-test-page__message ${
                error ? "notification-test-page__message--error" : ""
              }`}
            >
              {error ?? feedback}
            </div>
          )}

          <div className="notification-test-page__grid">
            {testActions.map((action) => (
              <article
                key={action.type}
                className="surface-card surface-card--padded notification-test-card"
              >
                <h2>{action.title}</h2>
                <p>{action.description}</p>
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={submittingType === action.type}
                  onClick={() => void handleSend(action.type)}
                >
                  {submittingType === action.type ? "Envoi..." : "Envoyer le DM"}
                </button>
              </article>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}
