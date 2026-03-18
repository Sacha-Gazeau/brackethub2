import { useTranslation } from "react-i18next";
import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { TournamentCardlogout } from "../../components/TournamentCard";
import {
  filterTournaments,
  type TournamentFilters,
  type TournamentItem,
} from "../../action/tournamentFilter";
import {
  initialTournamentFilters,
  loadTournamentPageGames,
  loadTournamentPageSession,
  loadTournamentPageTournaments,
  selectGameOptions,
  selectStatusOptions,
} from "../../action/tournamentPage";
import type { GameRef } from "../../action/tournamentData";
import { usePageSeo } from "../../action/usePageSeo";

export default function TournamentPage() {
  const { t } = useTranslation();
  const apiUrl = import.meta.env.VITE_API_URL;
  const [searchParams] = useSearchParams();
  const [tournaments, setTournaments] = useState<TournamentItem[]>([]);
  const [games, setGames] = useState<GameRef[]>([]);
  const [loading, setLoading] = useState(true);
  const [userId, setUserId] = useState<string | null>(null);
  const [filters, setFilters] = useState<TournamentFilters>(initialTournamentFilters);
  const scope = searchParams.get("scope");
  const isMineScope = scope === "mine";

  usePageSeo({
    title: "BracketHub | Tournaments",
    description: "Ontdek lopende en aankomende esports toernooien op BracketHub.",
  });

  useEffect(() => {
    const loadSession = async () => {
      setUserId(await loadTournamentPageSession());
    };

    void loadSession();
  }, []);

  useEffect(() => {
    const loadTournaments = async () => {
      setLoading(true);

      try {
        const loadedTournaments = await loadTournamentPageTournaments(
          isMineScope,
          userId,
        );
        setTournaments(loadedTournaments);
        setGames(await loadTournamentPageGames(apiUrl, loadedTournaments, t));
      } catch (error) {
        console.error("Tournament LOAD ERROR:", error);
        setTournaments([]);
        setGames([]);
      } finally {
        setLoading(false);
      }
    };

    void loadTournaments();
  }, [apiUrl, isMineScope, t, userId]);

  const visibleTournaments = useMemo(
    () => tournaments,
    [tournaments],
  );
  const filteredTournaments = useMemo(
    () => filterTournaments(visibleTournaments, filters),
    [filters, visibleTournaments],
  );
  const gameNameById = useMemo(
    () => new Map(games.map((game) => [String(game.id), game.name])),
    [games],
  );
  const gameOptions = useMemo(
    () => selectGameOptions(visibleTournaments),
    [visibleTournaments],
  );
  const statusOptions = useMemo(
    () => selectStatusOptions(visibleTournaments),
    [visibleTournaments],
  );

  const handleFilterChange = (
    field: keyof TournamentFilters,
    value: string,
  ) => {
    setFilters((previous) => ({
      ...previous,
      [field]: value,
    }));
  };

  const resetFilters = () => {
    setFilters(initialTournamentFilters);
  };

  return (
    <div className="tournaments-page">
      {/* HERO */}
      <section className="tournaments-hero page-block">
        <div className="tournaments-hero__inner page-shell page-header__inner">
          <div className="tournaments-hero__content page-header__content">
            <div>
              <h1 className="tournaments-hero__title page-header__title">
                {t("tournamentsPage.hero.title")}
              </h1>
              <p className="tournaments-hero__subtitle page-header__subtitle">
                {t("tournamentsPage.hero.subtitle")}
              </p>
            </div>

            <Link className="btn btn--primary" to="/tournaments/create">
              {t("tournamentsPage.hero.create")}
            </Link>
          </div>
        </div>
      </section>

      {/* FILTERS */}
      <section className="tournaments-filters page-block">
        <div className="tournaments-filters__inner page-shell">
          <div className="filters-card surface-card surface-card--padded">
            <div className="filters-card__top">
              <span className="filters-card__icon" aria-hidden="true">⏷</span>
              <h3 className="filters-card__title">{t("tournamentsPage.filter.title")}</h3>
            </div>

            <form className="filters-form">
              <label className="filters-field">
                <span className="filters-label">{t("tournamentsPage.filter.input1")}</span>
                <select
                  className="filters-select"
                  name="game"
                  value={filters.game}
                  onChange={(event) => handleFilterChange("game", event.target.value)}
                >
                  <option value="">—</option>
                  {gameOptions.map((gameIdOrName) => (
                    <option key={gameIdOrName} value={gameIdOrName}>
                      {gameNameById.get(gameIdOrName) ?? gameIdOrName}
                    </option>
                  ))}
                </select>
              </label>

              <label className="filters-field">
                <span className="filters-label">{t("tournamentsPage.filter.input2")}</span>
                <select
                  className="filters-select"
                  name="status"
                  value={filters.status}
                  onChange={(event) => handleFilterChange("status", event.target.value)}
                >
                  <option value="">—</option>
                  {statusOptions.map((status) => (
                    <option key={status} value={status}>
                      {status}
                    </option>
                  ))}
                </select>
              </label>

              <label className="filters-field">
                <span className="filters-label">{t("tournamentsPage.filter.input3")}</span>
                <input
                  className="filters-input"
                  type="date"
                  name="date"
                  value={filters.date}
                  onChange={(event) => handleFilterChange("date", event.target.value)}
                />
              </label>

              <div className="filters-actions">
                <button type="button" className="btn btn--outline" onClick={resetFilters}>
                  {t("tournamentsPage.filter.button")}
                </button>
              </div>
            </form>
            <div className="tournaments-found">
              <span className="tournaments-found__count">{filteredTournaments.length}</span>
              <span>toernooien gevonden</span>
            </div>
          </div>
        </div>
      </section>

      {/* CARDS */}
      <section className="tournaments-list">
        <div className="tournaments-list__inner page-shell">
          {loading ? (
            <p>{t("tournamentsPage.loading")}</p>
          ) : (
            <TournamentCardlogout
              tournaments={filteredTournaments}
              emptyState={
                isMineScope
                  ? {
                      title: t("tournamentsPage.emptyMine.title"),
                      description: t("tournamentsPage.emptyMine.description"),
                      actionLabel: t("tournamentsPage.emptyMine.action"),
                      actionTo: "/tournaments/create",
                    }
                  : undefined
              }
            />
          )}
        </div>
      </section>

      <div className="tournaments-spacer" />
    </div>
  );
}
