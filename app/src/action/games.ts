type Translate = (key: string, options?: Record<string, unknown>) => string;

export type GameSearchResult = {
  id: number;
  name: string;
  cover: {
    image_id: string | null;
  } | null;
};

export type GameDetail = {
  id: number;
  name: string;
  coverUrl: string | null;
};

type GameApiResult<T> = {
  data: T | null;
  error: string | null;
};

export function buildIgdbCoverUrl(imageId: string | null | undefined) {
  return imageId
    ? `https://images.igdb.com/igdb/image/upload/t_cover_big/${imageId}.jpg`
    : null;
}

export async function searchGames(
  apiUrl: string,
  query: string,
  t: Translate,
): Promise<GameApiResult<GameSearchResult[]>> {
  try {
    const response = await fetch(
      `${apiUrl}/api/games/search?query=${encodeURIComponent(query)}`,
    );
    const result = await response.json().catch(() => null);

    if (!response.ok) {
      return {
        data: null,
        error: result?.message ?? t("createTournamentPage.messages.gameSearchFailed"),
      };
    }

    return {
      data: (result as GameSearchResult[]) ?? [],
      error: null,
    };
  } catch {
    return {
      data: null,
      error: t("createTournamentPage.messages.connectionFailed"),
    };
  }
}

export async function getGameById(
  apiUrl: string,
  gameId: number,
  t: Translate,
): Promise<GameApiResult<GameDetail>> {
  try {
    const response = await fetch(`${apiUrl}/api/games/${gameId}`);
    const result = await response.json().catch(() => null);

    if (!response.ok) {
      return {
        data: null,
        error: result?.message ?? t("tournamentDetail.messages.gameLoadFailed"),
      };
    }

    return {
      data: (result as GameDetail) ?? null,
      error: null,
    };
  } catch {
    return {
      data: null,
      error: t("createTournamentPage.messages.connectionFailed"),
    };
  }
}
