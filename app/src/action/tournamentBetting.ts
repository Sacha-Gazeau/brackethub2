import { supabase } from "../lib/supabaseClient";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export type TournamentBet = {
  id: number;
  tournament_id: number;
  team_id: number;
  coins_bet: number;
  status: "pending" | "won" | "lost";
  paid_out: boolean;
  created_at: string;
  paid_out_at: string | null;
};

export type TournamentBetState = {
  betting_open: boolean;
  coins_balance: number;
  bet: TournamentBet | null;
};

type ApiResult = {
  ok: boolean;
  message: string;
  remainingCoins?: number;
};

export type PlaceTournamentBetPayload = {
  tournamentId: number;
  teamId: number;
  coinsBet: number;
};

export async function getAccessTokenForApi(t: Translate) {
  const {
    data: { session },
    error: authError,
  } = await supabase.auth.getSession();

  if (authError || !session) {
    return {
      token: null as string | null,
      error: authError?.message ?? t("tournamentBetting.errors.loginRequired"),
    };
  }

  const nowInSeconds = Math.floor(Date.now() / 1000);
  const expiresAt = session.expires_at ?? 0;
  if (expiresAt <= nowInSeconds + 30) {
    const { data: refreshedData, error: refreshError } = await supabase.auth.refreshSession();

    if (refreshError || !refreshedData.session?.access_token) {
      return {
        token: null as string | null,
        error:
          refreshError?.message ?? t("tournamentBetting.errors.sessionExpired"),
      };
    }

    return {
      token: refreshedData.session.access_token,
      error: null as string | null,
    };
  }

  return {
    token: session.access_token,
    error: null as string | null,
  };
}

export async function loadTournamentBetState(
  apiUrl: string,
  tournamentId: number,
  t: Translate,
): Promise<{ data: TournamentBetState | null; error: string | null }> {
  const { token, error } = await getAccessTokenForApi(t);
  if (!token) {
    return {
      data: null,
      error,
    };
  }

  try {
    const response = await fetch(`${apiUrl}/api/tournaments/${tournamentId}/bets/me`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    const result = await readApiPayload(response);
    if (!response.ok) {
      const backendMessage = buildBackendMessage(result);
      return {
        data: null,
        error: mapTournamentBetErrorMessage(
          backendMessage,
          t,
        ),
      };
    }

    return {
      data: (result as TournamentBetState) ?? null,
      error: null,
    };
  } catch {
    return {
      data: null,
      error: t("tournamentBetting.errors.connectionFailed"),
    };
  }
}

export async function placeTournamentBet(
  apiUrl: string,
  payload: PlaceTournamentBetPayload,
  t: Translate,
): Promise<ApiResult> {
  const { token, error } = await getAccessTokenForApi(t);
  if (!token) {
    return {
      ok: false,
      message: error ?? t("tournamentBetting.errors.loginRequired"),
    };
  }

  try {
    const response = await fetch(`${apiUrl}/api/tournaments/bets`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(payload),
    });

    const result = await readApiPayload(response);
    if (!response.ok) {
      const backendMessage = buildBackendMessage(result);
      return {
        ok: false,
        message: mapTournamentBetErrorMessage(
          backendMessage,
          t,
        ),
      };
    }

    return {
      ok: true,
      message:
        (typeof result?.message === "string" && result.message) ||
        t("tournamentBetting.messages.success"),
      remainingCoins:
        typeof result?.remaining_coins === "number"
          ? result.remaining_coins
          : undefined,
    };
  } catch {
    return {
      ok: false,
      message: t("tournamentBetting.errors.connectionFailed"),
    };
  }
}

export function validateTournamentBetAmount(
  value: string,
  availableCoins: number | null,
  t: Translate,
): string | null {
  if (!value.trim()) {
    return t("tournamentBetting.errors.amountRequired");
  }

  if (!/^\d+$/.test(value.trim())) {
    return t("tournamentBetting.errors.amountInvalid");
  }

  const coinsBet = Number(value);
  if (!Number.isInteger(coinsBet) || coinsBet <= 0) {
    return t("tournamentBetting.errors.amountInvalid");
  }

  if (availableCoins !== null && coinsBet > availableCoins) {
    return t("tournamentBetting.errors.insufficientCoins");
  }

  return null;
}

function mapTournamentBetErrorMessage(
  message: string | null,
  t: Translate,
): string {
  if (!message) {
    return t("tournamentBetting.errors.submitFailed");
  }

  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes("tournament not found")) {
    return t("tournamentBetting.errors.tournamentNotFound");
  }

  if (normalizedMessage.includes("upcoming tournaments")) {
    return t("tournamentBetting.errors.bettingClosed");
  }

  if (normalizedMessage.includes("valid participant")) {
    return t("tournamentBetting.errors.invalidTeam");
  }

  if (normalizedMessage.includes("already exists")) {
    return t("tournamentBetting.errors.betAlreadyExists");
  }

  if (normalizedMessage.includes("greater than zero")) {
    return t("tournamentBetting.errors.amountInvalid");
  }

  if (normalizedMessage.includes("insufficient coins")) {
    return t("tournamentBetting.errors.insufficientCoins");
  }

  if (normalizedMessage.includes("profile not found")) {
    return t("tournamentBetting.errors.profileNotFound");
  }

  if (normalizedMessage.includes("relation \"bets\" does not exist")) {
    return t("tournamentBetting.errors.betsTableMissing");
  }

  if (normalizedMessage.includes("column") && normalizedMessage.includes("bets")) {
    return t("tournamentBetting.errors.betsMigrationOutdated");
  }

  if (
    normalizedMessage.includes("connection string") ||
    normalizedMessage.includes("database") ||
    normalizedMessage.includes("timeout") ||
    normalizedMessage.includes("refused")
  ) {
    return t("tournamentBetting.errors.backendDatabase");
  }

  return message;
}

async function readApiPayload(response: Response) {
  const rawText = await response.text();
  if (!rawText) {
    return null;
  }

  try {
    return JSON.parse(rawText) as Record<string, unknown>;
  } catch {
    return {
      message: rawText,
    };
  }
}

function buildBackendMessage(payload: Record<string, unknown> | null) {
  if (!payload) {
    return null;
  }

  const message =
    typeof payload.message === "string" ? payload.message.trim() : null;
  const error = typeof payload.error === "string" ? payload.error.trim() : null;

  if (message && error) {
    return `${message} ${error}`;
  }

  return message ?? error;
}
