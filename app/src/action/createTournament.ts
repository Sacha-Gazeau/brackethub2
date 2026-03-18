import { supabase } from "../lib/supabaseClient";

type Translate = (key: string, options?: Record<string, unknown>) => string;

export const TEAM_SIZE_OPTIONS = [2, 4, 8, 16, 32, 64] as const;

export type FormatOption = {
  id: number;
  name: string;
};

export type CreateTournamentRequest = {
  name: string;
  game_igdb_id: number;
  format: number;
  max_teams: number;
  min_teams: number;
  players_per_team: number;
  start_date: string;
  end_date: string;
  privacy: "public" | "friends" | "official";
  final_format: number | null;
  description: string | null;
};

export type CreateTournamentFormState = {
  name: string;
  gameSearch: string;
  gameIgdbId: string;
  gameName: string;
  gameCoverUrl: string;
  startDate: string;
  endDate: string;
  startTime: string;
  teamCount: string;
  minTeams: string;
  playersPerTeam: string;
  privacy: "public" | "friends";
  formatId: string;
  finalFormatId: string;
  description: string;
};

export type CreateTournamentFieldErrorKey =
  | "name"
  | "gameSearch"
  | "formatId"
  | "teamCount"
  | "minTeams"
  | "playersPerTeam"
  | "startDate"
  | "startTime"
  | "endDate";

export type CreateTournamentFieldErrors = Partial<
  Record<CreateTournamentFieldErrorKey, string>
>;

export type ApiResult = {
  ok: boolean;
  message: string;
  slug?: string;
  fieldErrors?: CreateTournamentFieldErrors;
};

export const initialCreateTournamentFormState: CreateTournamentFormState = {
  name: "",
  gameSearch: "",
  gameIgdbId: "",
  gameName: "",
  gameCoverUrl: "",
  startDate: "",
  endDate: "",
  startTime: "",
  teamCount: "",
  minTeams: "",
  playersPerTeam: "",
  privacy: "public",
  formatId: "",
  finalFormatId: "",
  description: "",
};

export function isValidTeamSize(value: string) {
  const parsedValue = Number(value);

  return TEAM_SIZE_OPTIONS.includes(parsedValue as (typeof TEAM_SIZE_OPTIONS)[number]);
}

export function getMinTeamOptions(maxTeams: string) {
  if (!isValidTeamSize(maxTeams)) {
    return [] as number[];
  }

  const parsedMaxTeams = Number(maxTeams);
  return TEAM_SIZE_OPTIONS.filter((teamSize) => teamSize <= parsedMaxTeams);
}

export function normalizeMinTeams(minTeams: string, maxTeams: string) {
  const minTeamOptions = getMinTeamOptions(maxTeams);

  if (minTeamOptions.length === 0) {
    return "";
  }

  if (isValidTeamSize(minTeams) && minTeamOptions.includes(Number(minTeams))) {
    return minTeams;
  }

  return String(minTeamOptions[minTeamOptions.length - 1]);
}

export async function loadCreateTournamentOptions(t: Translate) {
  const formatsResponse = await supabase
    .from("formats")
    .select("id, name")
    .order("name", { ascending: true });

  if (formatsResponse.error) {
    return {
      error: formatsResponse.error.message ?? t("createTournamentPage.messages.loadOptionsFailed"),
      formats: [] as FormatOption[],
    };
  }

  return {
    error: null as string | null,
    formats: (formatsResponse.data as FormatOption[]) ?? [],
  };
}

export function validateCreateTournamentForm(
  values: CreateTournamentFormState,
  t: Translate,
): { fieldErrors: CreateTournamentFieldErrors; message: string | null } {
  const fieldErrors: CreateTournamentFieldErrors = {};

  if (!values.name.trim()) {
    fieldErrors.name = t("createTournamentPage.errors.nameRequired");
  }

  if (!values.gameIgdbId) {
    fieldErrors.gameSearch = t("createTournamentPage.errors.gameRequired");
  }

  if (!values.formatId) {
    fieldErrors.formatId = t("createTournamentPage.errors.formatRequired");
  }

  if (!values.startDate) {
    fieldErrors.startDate = t("createTournamentPage.errors.startDateRequired");
  }

  if (!values.startTime) {
    fieldErrors.startTime = t("createTournamentPage.errors.startTimeRequired");
  }

  if (!values.endDate) {
    fieldErrors.endDate = t("createTournamentPage.errors.endDateRequired");
  }

  if (!values.teamCount) {
    fieldErrors.teamCount = t("createTournamentPage.errors.teamCountRequired");
  } else if (!isValidTeamSize(values.teamCount)) {
    fieldErrors.teamCount = t("createTournamentPage.errors.teamCountMin");
  }

  const minTeams = Number(values.minTeams);
  const minTeamOptions = getMinTeamOptions(values.teamCount);
  if (!values.minTeams) {
    fieldErrors.minTeams = t("createTournamentPage.errors.minTeamsRequired");
  } else if (!isValidTeamSize(values.minTeams)) {
    fieldErrors.minTeams = t("createTournamentPage.errors.minTeamsMin");
  } else if (!minTeamOptions.includes(minTeams)) {
    fieldErrors.minTeams = t("createTournamentPage.errors.minTeamsGreaterThanMax");
  }

  const playersCount = Number(values.playersPerTeam);
  if (!values.playersPerTeam) {
    fieldErrors.playersPerTeam = t("createTournamentPage.errors.playersPerTeamRequired");
  } else if (Number.isNaN(playersCount) || playersCount < 1) {
    fieldErrors.playersPerTeam = t("createTournamentPage.errors.playersPerTeamMin");
  }

  const hasErrors = Object.keys(fieldErrors).length > 0;
  return {
    fieldErrors,
    message: hasErrors ? t("createTournamentPage.messages.fixFields") : null,
  };
}

export function buildCreateTournamentPayload(
  values: CreateTournamentFormState,
): CreateTournamentRequest {
  const startDateTime = new Date(
    `${values.startDate}T${values.startTime}:00`,
  ).toISOString();
  const endDateTime = new Date(`${values.endDate}T23:59:59`).toISOString();

  return {
    name: values.name.trim(),
    game_igdb_id: Number(values.gameIgdbId),
    format: Number(values.formatId),
    max_teams: Number(values.teamCount),
    min_teams: Number(values.minTeams),
    players_per_team: Number(values.playersPerTeam),
    start_date: startDateTime,
    end_date: endDateTime,
    privacy: values.privacy,
    final_format: values.finalFormatId ? Number(values.finalFormatId) : null,
    description: values.description.trim() ? values.description.trim() : null,
  };
}

export async function getCreateTournamentAccessToken(t: Translate) {
  const {
    data: { session },
    error: authError,
  } = await supabase.auth.getSession();

  if (authError || !session) {
    return {
      token: null as string | null,
      error: authError?.message ?? t("createTournamentPage.messages.loginRequired"),
    };
  }

  const nowInSeconds = Math.floor(Date.now() / 1000);
  const expiresAt = session.expires_at ?? 0;
  const shouldRefreshToken = expiresAt <= nowInSeconds + 30;

  if (shouldRefreshToken) {
    const { data: refreshedData, error: refreshError } = await supabase.auth.refreshSession();

    if (refreshError || !refreshedData.session?.access_token) {
      return {
        token: null as string | null,
        error: refreshError?.message ?? t("createTournamentPage.messages.sessionExpired"),
      };
    }

    return {
      token: refreshedData.session.access_token,
      error: null as string | null,
    };
  }

  return { token: session.access_token, error: null as string | null };
}

export async function submitCreateTournament(
  apiUrl: string,
  accessToken: string,
  payload: CreateTournamentRequest,
  t: Translate,
): Promise<ApiResult> {
  try {
    const response = await fetch(`${apiUrl}/api/tournaments`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(payload),
    });

    const result = await response.json().catch(() => null);
    if (!response.ok) {
      return {
        ok: false,
        message: result?.message ?? t("createTournamentPage.messages.createFailed"),
        fieldErrors: mapBackendFieldErrors(result?.errors),
      };
    }

    return {
      ok: true,
      message: result?.message ?? t("createTournamentPage.messages.created"),
      slug: result?.slug,
    };
  } catch {
    return { ok: false, message: t("createTournamentPage.messages.connectionFailed") };
  }
}

function mapBackendFieldErrors(rawErrors: unknown): CreateTournamentFieldErrors {
  if (!rawErrors || typeof rawErrors !== "object") {
    return {};
  }

  const backendErrors = rawErrors as Record<string, unknown>;
  const fieldErrors: CreateTournamentFieldErrors = {};

  const fieldMap: Record<string, CreateTournamentFieldErrorKey> = {
    name: "name",
    game: "gameSearch",
    gameId: "gameSearch",
    game_igdb_id: "gameSearch",
    gameIgdbId: "gameSearch",
    format: "formatId",
    formatId: "formatId",
    max_teams: "teamCount",
    teamCount: "teamCount",
    min_teams: "minTeams",
    minTeams: "minTeams",
    players_per_team: "playersPerTeam",
    playersPerTeam: "playersPerTeam",
    start_date: "startDate",
    startDate: "startDate",
    start_time: "startTime",
    startTime: "startTime",
    end_date: "endDate",
    endDate: "endDate",
  };

  for (const [backendKey, value] of Object.entries(backendErrors)) {
    const frontendKey = fieldMap[backendKey];
    if (!frontendKey || typeof value !== "string" || !value.trim()) {
      continue;
    }

    fieldErrors[frontendKey] = value;
  }

  return fieldErrors;
}
