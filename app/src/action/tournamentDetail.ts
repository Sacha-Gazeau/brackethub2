export function getRelationName(value: string | number | null | undefined) {
  if (value === null || value === undefined) {
    return "-";
  }

  return String(value);
}

export function getPrivacyLabel(
  privacy: string | null | undefined,
  t: (key: string) => string,
) {
  if (privacy === "official") {
    return t("createTournamentPage.types.officialTitle");
  }

  if (privacy === "public") {
    return t("createTournamentPage.types.publicTitle");
  }

  if (privacy === "friends") {
    return t("createTournamentPage.types.friendsTitle");
  }

  return privacy ?? "-";
}
