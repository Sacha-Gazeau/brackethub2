type Translate = (key: string, options?: Record<string, unknown>) => string;

export type SubmitTeamRequestPayload = {
  tournamentId: number;
  captainId: string;
  teamName: string;
  playerNames: string[];
};

export type SubmitTeamRequestResult = {
  ok: boolean;
  message: string;
};

export function validateTeamRequestForm(
  teamName: string,
  playerNames: string[],
  t: Translate,
): string | null {
  if (!teamName.trim()) {
    return t("teamRequestModal.errors.teamNameRequired");
  }

  if (playerNames.some((playerName) => !playerName.trim())) {
    return t("teamRequestModal.errors.playerNamesRequired");
  }

  return null;
}

export async function submitTeamRequest(
  apiUrl: string,
  payload: SubmitTeamRequestPayload,
  t: Translate,
): Promise<SubmitTeamRequestResult> {
  try {
    const response = await fetch(`${apiUrl}/api/team-requests`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    });

    const result = await response.json().catch(() => null);
    if (!response.ok) {
      const backendMessage =
        typeof result?.message === "string" ? result.message : null;
      const backendError =
        typeof result?.error === "string" ? result.error : null;

      return {
        ok: false,
        message: mapTeamRequestErrorMessage(
          backendMessage && backendError
            ? `${backendMessage} ${backendError}`
            : backendMessage ?? backendError,
          t,
        ),
      };
    }

    return {
      ok: true,
      message:
        mapTeamRequestErrorMessage(
          typeof result?.message === "string" ? result.message : null,
          t,
        ) ?? t("teamRequestModal.messages.success"),
    };
  } catch {
    return {
      ok: false,
      message: t("teamRequestModal.errors.connectionFailed"),
    };
  }
}

function mapTeamRequestErrorMessage(
  message: string | null,
  t: Translate,
): string {
  if (!message) {
    return t("teamRequestModal.errors.submitFailed");
  }

  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes("tournament not found")) {
    return t("teamRequestModal.errors.tournamentNotFound");
  }

  if (normalizedMessage.includes("tournament is full")) {
    return t("teamRequestModal.errors.tournamentFull");
  }

  if (normalizedMessage.includes("pending request")) {
    return t("teamRequestModal.errors.pendingAlreadyExists");
  }

  if (normalizedMessage.includes("rejection limit")) {
    return t("teamRequestModal.errors.rejectionLimitReached");
  }

  if (normalizedMessage.includes("player count")) {
    return t("teamRequestModal.errors.invalidPlayerCount");
  }

  if (normalizedMessage.includes("unable to create the team request right now")) {
    return t("teamRequestModal.errors.submitFailed");
  }

  return message;
}
