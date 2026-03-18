import { getAccessTokenForApi } from "./tournamentBetting";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type NotificationTestType =
  | "welcome"
  | "join_request"
  | "join_accept"
  | "join_reject"
  | "reminder"
  | "bet_won"
  | "bet_lost"
  | "reward";

export async function sendNotificationTest(
  apiUrl: string | undefined,
  type: NotificationTestType,
  t: Translate,
) {
  if (!apiUrl) {
    return {
      ok: false,
      message: "VITE_API_URL ontbreekt.",
    };
  }

  const { token, error } = await getAccessTokenForApi(t);
  if (!token) {
    return {
      ok: false,
      message: error ?? "Je moet ingelogd zijn.",
    };
  }

  try {
    const response = await fetch(`${apiUrl}/api/notifications/test-dm`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ type }),
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      return {
        ok: false,
        message:
          (typeof payload?.message === "string" && payload.message) ||
          "De testmelding kon niet verzonden worden.",
      };
    }

    return {
      ok: true,
      message:
        (typeof payload?.message === "string" && payload.message) ||
        "Testmelding verzonden.",
    };
  } catch {
    return {
      ok: false,
      message: "Verbinding met de backend mislukt.",
    };
  }
}
