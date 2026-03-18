import { useTranslation } from "react-i18next";
import { TournamentCardlogout } from "../components/TournamentCard";
import { Link } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";
import type { TournamentItem } from "../action/tournamentFilter";
import { useDailyReward } from "../action/useDailyReward";
import {
  loadHomeProfile,
  loadHomeSession,
  loadHomeTournaments,
  selectLatestOfficialTournaments,
  selectMyLatestTournaments,
  selectUpcomingOfficialTournaments,
  subscribeHomeSession,
  type HomeProfile,
} from "../action/home";
import { usePageSeo } from "../action/usePageSeo";

export default function Home() {
  const { t } = useTranslation();
  const apiUrl = import.meta.env.VITE_API_URL;

  const [isLogin, setIsLogin] = useState(false);
  const [userId, setUserId] = useState<string | null>(null);
  const [profile, setProfile] = useState<HomeProfile | null>(null);
  const [tournaments, setTournaments] = useState<TournamentItem[]>([]);
  const {
    coins,
    countdown,
    isClaiming,
    claimMessage,
    canClaimDaily,
    claimDaily,
  } = useDailyReward({
    userId,
    apiUrl,
    initialCoins: 0,
  });

  usePageSeo({
    title: "BracketHub | Home",
    description: "BracketHub is het platform voor esports toernooien, brackets en betting.",
  });

  useEffect(() => {
    const initializeSession = async () => {
      const session = await loadHomeSession();
      setIsLogin(session.isLogin);
      setUserId(session.userId);
    };

    void initializeSession();

    return subscribeHomeSession((session) => {
      setIsLogin(session.isLogin);
      setUserId(session.userId);
    });
  }, []);

  useEffect(() => {
    if (!userId) return;

    const loadProfile = async () => {
      try {
        setProfile(await loadHomeProfile(userId));
      } catch (error) {
        console.error("PROFILE LOAD ERROR:", error);
        setProfile(null);
      }
    };

    void loadProfile();
  }, [userId]);

  useEffect(() => {
    const loadTournaments = async () => {
      try {
        setTournaments(await loadHomeTournaments());
      } catch (error) {
        console.error("TOURNAMENT LOAD ERROR:", error);
        setTournaments([]);
      }
    };

    void loadTournaments();
  }, []);

  const myLatestTournaments = useMemo(
    () => selectMyLatestTournaments(tournaments, userId),
    [tournaments, userId],
  );
  const latestOfficialTournaments = useMemo(
    () => selectLatestOfficialTournaments(tournaments),
    [tournaments],
  );
  const upcomingOfficialTournaments = useMemo(
    () => selectUpcomingOfficialTournaments(tournaments),
    [tournaments],
  );

  if (isLogin) {
    return (
      <div className="home home--auth">
        <section className="home-dashboard page-block">
          <div className="home-dashboard__inner">
            <div className="home-dashboard__grid">
              <div className="dashCard dashCard--welcome">
                <div className="dashCard__title">
                  👤 {t("homePage.login.title")} {profile?.username}!
                </div>
                <div className="dashCard__sub">
                  {t("homePage.login.subtitle")}
                </div>

                <div className="dashCard__actions">
                  <Link className="btn btn--outline" to="/tournaments">
                    {t("homePage.hero.viewTournaments")}
                  </Link>
                  <Link className="btn btn--primary" to="/tournaments/create">
                    {t("homePage.login.createTournament")}
                  </Link>
                </div>
              </div>

              <div className="dashCard dashCard--coins">
                <div className="dashCard__title">
                  🪙 {t("homePage.login.coinsTitle")}
                </div>
                <div className="dashCoins">
                  <div className="dashCoins__value">{coins}</div>
                  <div className="dashCoins__label">
                    {t("homePage.login.coins")}
                  </div>
                </div>
                <button
                  className="btn btn--outline btn--full"
                  type="button"
                  onClick={claimDaily}
                  disabled={isClaiming || !canClaimDaily}
                >
                  {isClaiming
                    ? t("homePage.login.claiming")
                    : canClaimDaily
                      ? t("homePage.login.coinsButton")
                      : t("homePage.login.nextClaim", { countdown })}
                </button>
                {claimMessage && (
                  <p className="dashCoins__label">{claimMessage}</p>
                )}
              </div>
            </div>
          </div>
        </section>

        <section className="home-section section-block page-block">
          <div className="home-section__header section-header">
            <h2 className="home-section__title section-title">
              {t("homePage.myTournaments")}
            </h2>
            <Link className="home-section__link section-link" to="/tournaments?scope=mine">
              {t("homePage.viewAll")} →
            </Link>
          </div>
          <TournamentCardlogout
            tournaments={myLatestTournaments}
            emptyState={{
              title: t("tournamentsPage.emptyMine.title"),
              description: t("tournamentsPage.emptyMine.description"),
              actionLabel: t("tournamentsPage.emptyMine.action"),
              actionTo: "/tournaments/create",
            }}
          />
        </section>

        <section className="home-section section-block page-block">
          <div className="home-section__header section-header">
            <h2 className="home-section__title section-title">
              {t("homePage.popularTournaments")}
            </h2>
            <Link className="home-section__link section-link" to="/tournaments">
              {t("homePage.viewAll")} →
            </Link>
          </div>
          <TournamentCardlogout tournaments={latestOfficialTournaments} />
        </section>

        <section className="home-section section-block page-block">
          <div className="home-section__header section-header">
            <h2 className="home-section__title section-title">
              📅 {t("homePage.upcomingTournaments")}
            </h2>
          </div>
          <TournamentCardlogout tournaments={upcomingOfficialTournaments} />
        </section>
      </div>
    );
  }
  return (
    <div className="home">
      <section className="home-hero page-block">
        <div className="home-hero__inner page-header__inner">
          <div className="home-hero__left">
            <h1 className="home-hero__title page-header__title">{t("homePage.hero.title")}</h1>
            <p className="home-hero__subtitle page-header__subtitle page-header__subtitle--spaced">{t("homePage.hero.subtitle")}</p>

            <div className="home-hero__actions">
              <Link className="btn btn--outline" to="/tournaments">
                {t("homePage.hero.viewTournaments")}
              </Link>
              <Link className="btn btn--primary" to="/login">
                {t("homePage.hero.loginWithDiscord")}
              </Link>
            </div>
          </div>

          <div className="home-hero__right">
            <div className="home-hero__art" aria-hidden="true">
              <div className="home-hero__icon">🏆</div>
              <div className="home-hero__artText">{t("navigation.title")}</div>
            </div>
          </div>
        </div>
      </section>

      {/* POPULAR */}
      <section className="home-section section-block page-block">
        <div className="home-section__header section-header">
          <h2 className="home-section__title section-title">
            {t("homePage.popularTournaments")}
          </h2>
          <Link className="home-section__link section-link" to="/tournaments">
            {t("homePage.viewAll")} →
          </Link>
        </div>

        <TournamentCardlogout tournaments={latestOfficialTournaments} />
      </section>

      <section className="home-section section-block page-block">
        <div className="home-section__header section-header">
          <h2 className="home-section__title section-title">
            📅 {t("homePage.upcomingTournaments")}
          </h2>
        </div>

        <TournamentCardlogout tournaments={upcomingOfficialTournaments} />
      </section>
    </div>
  );
}
