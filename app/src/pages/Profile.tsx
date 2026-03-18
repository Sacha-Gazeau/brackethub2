import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useEffect, useMemo, useState } from "react";
import { useDailyReward } from "../action/useDailyReward";
import { supabase } from "../lib/supabaseClient";
import {
  getProfileBetWinRate,
  getProfileSpentCoins,
  loadProfilePageData,
  type ProfileBetHistoryItem,
  type ProfileCreatedTournament,
  type ProfileSummary,
} from "../action/profile";
import { getTournamentStatus } from "../action/tournamentStatus";
import { usePageSeo } from "../action/usePageSeo";

const apiUrl = import.meta.env.VITE_API_URL;

export default function Profile() {
  const { t } = useTranslation();
  const [userId, setUserId] = useState<string | null>(null);
  const [profile, setProfile] = useState<ProfileSummary | null>(null);
  const [createdTournaments, setCreatedTournaments] = useState<
    ProfileCreatedTournament[]
  >([]);
  const [bets, setBets] = useState<ProfileBetHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
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
    title: "BracketHub | Profile",
    description:
      "Bekijk je profielstatistieken, coins, lifetime coins en toernooiactiviteit op BracketHub.",
  });

  useEffect(() => {
    const getSession = async () => {
      const { data } = await supabase.auth.getSession();
      setUserId(data.session?.user?.id ?? null);
    };

    void getSession();

    const { data: listener } = supabase.auth.onAuthStateChange(
      (_event, session) => {
        setUserId(session?.user?.id ?? null);
      },
    );

    return () => {
      listener.subscription.unsubscribe();
    };
  }, []);

  useEffect(() => {
    if (!userId) {
      setProfile(null);
      setCreatedTournaments([]);
      setBets([]);
      setLoading(false);
      return;
    }

    const loadProfile = async () => {
      setLoading(true);

      try {
        const data = await loadProfilePageData(userId, apiUrl, t);
        setProfile(data.profile);
        setCreatedTournaments(data.createdTournaments);
        setBets(data.bets);
      } catch (error) {
        console.error("PROFILE LOAD ERROR:", error);
        setProfile(null);
        setCreatedTournaments([]);
        setBets([]);
      } finally {
        setLoading(false);
      }
    };

    void loadProfile();
  }, [t, userId]);

  const betWinRate = useMemo(() => getProfileBetWinRate(bets), [bets]);
  const spentCoins = useMemo(() => getProfileSpentCoins(bets), [bets]);

  if (loading) {
    return (
      <div className="profilePage">
        <p>{t("profilePage.loading")}</p>
      </div>
    );
  }

  return (
    <div className="profilePage">
      <section className="profileTop page-block">
        <div className="profileTop__inner page-shell page-header__inner">
          <div className="profileIdentity surface-card surface-card--padded">
            <div className="profileAvatar" aria-hidden="true">
              <img
                src={profile?.avatar ?? "/discord.webp"}
                alt={profile?.username ?? t("profilePage.loading")}
              />
            </div>

            <div className="profileIdentity__meta">
              <h1 className="profileName">
                {profile?.username ?? t("profilePage.loading")}
              </h1>
              <div className="profileLinked">
                ● {t("profilePage.connected")}
              </div>

              <div className="profileCounts">
                <div>
                  <div className="profileCounts__label">
                    {t("profilePage.counts.created")}
                  </div>
                  <div className="profileCounts__value">
                    {createdTournaments.length}
                  </div>
                </div>
              </div>
            </div>

            <div className="dashCard dashCard--coins">
              <div className="dashCard__title">
                {t("profilePage.coinsCard.title")}
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

      <section className="profileContent section-block">
        <div className="profileContent__inner page-shell">
          <div className="profileCol">
            <div className="profilePanel surface-card">
              <div className="panelHeader">
                <h2>{t("profilePage.sections.created")}</h2>
              </div>

              <div className="panelList">
                {createdTournaments.map((tour) => (
                  <div key={tour.id} className="listRow">
                    <div className="listRow__main">
                      <div className="listRow__title">{tour.name}</div>
                      <div className="listRow__sub">
                        {t("profilePage.participants")} {tour.current_teams}/
                        {tour.max_teams}
                      </div>
                      <Link
                        className="btn btn--ghost btn--sm listRow__link"
                        to={`/tournament/${tour.slug ?? tour.id}`}
                      >
                        {t("profilePage.actions.viewTournament")}
                      </Link>
                    </div>
                    <div
                      className={`statusPill statusPill--${getTournamentStatus(tour)}`}
                    >
                      {getTournamentStatus(tour)}
                    </div>
                  </div>
                ))}

                {createdTournaments.length === 0 && (
                  <div className="panelEmpty">
                    {t("profilePage.empty.created")}
                  </div>
                )}

                <Link
                  className="btn btn--ghost btn--full panelCta"
                  to="/tournaments/create"
                >
                  {t("profilePage.actions.createTournament")}
                </Link>
              </div>
            </div>
          </div>

          <div className="profileCol">
            <div className="profilePanel surface-card">
              <div className="panelHeader">
                <h2>{t("profilePage.sections.bets")}</h2>
              </div>

              <div className="table">
                <div className="tableHead">
                  <div>{t("profilePage.table.tournament")}</div>
                  <div>{t("profilePage.table.bet")}</div>
                  <div>{t("profilePage.table.stake")}</div>
                  <div>{t("profilePage.table.status")}</div>
                </div>

                {bets.map((bet) => (
                  <div key={bet.id} className="tableRow">
                    <div>{bet.tournamentName}</div>
                    <div>{bet.prediction}</div>
                    <div>
                      {t("profilePage.table.stakeValue", { value: bet.stake })}
                    </div>
                    <div
                      className={
                        bet.result === "won"
                          ? "win"
                          : bet.result === "lost"
                            ? "lose"
                            : ""
                      }
                    >
                      {bet.result === "won"
                        ? t("profilePage.table.resultWon", {
                            payout: bet.payout ?? 0,
                          })
                        : bet.result === "lost"
                          ? t("profilePage.table.resultLost")
                          : t("profilePage.table.resultPending")}
                    </div>
                  </div>
                ))}

                {bets.length === 0 && (
                  <div className="panelEmpty panelEmpty--table">
                    {t("profilePage.empty.bets")}
                  </div>
                )}
              </div>

              <Link
                className="btn btn--ghost btn--full panelCta panelCta--center"
                to="/tournaments"
              >
                {t("profilePage.actions.viewAllBets")}
              </Link>
            </div>

            <div className="profilePanel surface-card">
              <div className="panelHeader">
                <h2>{t("profilePage.sections.stats")}</h2>
              </div>

              <div className="statsBox">
                <div className="statLine">
                  <span>{t("profilePage.stats.earned")}</span>
                  <strong>
                    {profile?.lifetimecoins?.toLocaleString("nl-BE") ?? "0"}
                  </strong>
                </div>
                <div className="statLine">
                  <span>{t("profilePage.stats.spent")}</span>
                  <strong>{spentCoins.toLocaleString("nl-BE")}</strong>
                </div>
                <div className="statLine">
                  <span>{t("profilePage.stats.winRate")}</span>
                  <strong>{betWinRate}%</strong>
                </div>
                <div className="statLine">
                  <span>{t("profilePage.stats.currentBalance")}</span>
                  <strong>{coins.toLocaleString("nl-BE")}</strong>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
