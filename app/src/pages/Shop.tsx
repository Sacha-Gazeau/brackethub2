import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import RewardCard from "../components/reward";
import { supabase } from "../lib/supabaseClient";
import { usePageSeo } from "../action/usePageSeo";

interface Reward {
  id: number;
  name: string;
  game: number;
  description: string;
  price: number;
  type: string;
  stock: number;
  image?: string | null;
}

export default function Shop() {
  const { t } = useTranslation();
  usePageSeo({
    title: "BracketHub | Shop",
    description:
      "Utilise tes coins BracketHub pour obtenir des recompenses et consulter les conditions de la boutique.",
  });

  const userCoins = 0;

  const [rewards, setRewards] = useState<Reward[]>([]);

  useEffect(() => {
    const loadRewards = async () => {
      const { data, error } = await supabase.from("rewards").select("*");

      if (error) {
        console.error("REWARDS LOAD ERROR:", error);
        return;
      }

      setRewards(data ?? []);
    };

    loadRewards();
  }, []);

  return (
    <div className="shop-page">
      <section className="page-block page-header">
        <div className="page-shell page-header__inner">
          <div className="page-header__content">
            <div>
              <h1 className="page-header__title">
                🛍️ {t("shopPage.title")}
              </h1>
              <p className="page-header__subtitle">
                {t("shopPage.subtitle")}
              </p>
            </div>
          </div>
        </div>
        <div className="shop-balance surface-card surface-card--padded">
          <div className="shop-balance__left">
            <div className="shop-balance__label">
              {t("shopPage.availableCoins")}
            </div>
            <div className="shop-balance__value">{userCoins}</div>
          </div>
          <button className="btn btn--outline" type="button">
            {t("shopPage.getMoreCoins")}
          </button>
        </div>
      </section>

      <section className="shop-gridSection">
        <div className="shop-gridSection__inner page-shell">
          <div className="cards">
            {rewards.map((reward) => (
              <RewardCard key={reward.id} reward={reward} />
            ))}
          </div>

          <div className="shop-info">
            <div className="shop-box surface-card surface-card--padded">
              <h3 className="shop-box__title">
                {t("shopPage.howItWorks.title")}
              </h3>
              <ol className="shop-box__list">
                <li>{t("shopPage.howItWorks.step1")}</li>
                <li>{t("shopPage.howItWorks.step2")}</li>
                <li>{t("shopPage.howItWorks.step3")}</li>
                <li>{t("shopPage.howItWorks.step4")}</li>
              </ol>
            </div>

            <div className="shop-box surface-card surface-card--padded">
              <h3 className="shop-box__title">{t("shopPage.terms.title")}</h3>
              <ul className="shop-box__list">
                <li>{t("shopPage.terms.item1")}</li>
                <li>{t("shopPage.terms.item2")}</li>
                <li>{t("shopPage.terms.item3")}</li>
                <li>{t("shopPage.terms.item4")}</li>
              </ul>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
