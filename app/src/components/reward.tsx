

import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

type Reward = {
  id: number;
  name: string;
  description: string;
  price: number;
  type: string;
  stock: number;
  image?: string | null;
};

type RewardCardProps = {
  reward: Reward;
};

export default function RewardCard({ reward }: RewardCardProps) {
  const { t } = useTranslation();

  const isLogin = false;

  const isSoldOut = reward.stock <= 0;

  return (
    <div className="card">
      <div className="card-top">
        <span className={`badge ${isSoldOut ? "badge-soldout" : "badge-available"}`}>
          {isSoldOut ? t("rewardCard.soldOut") : t("rewardCard.available")}
        </span>

        <span className="format">{reward.type}</span>
      </div>

      <h3>{reward.name}</h3>
      <p className="date">{reward.description}</p>
      <p className="teams">
        {t("rewardCard.priceStock", { price: reward.price, stock: reward.stock })}
      </p>

      {isLogin ? (
        <div className="card-actions">
          <Link className="btn btn--primary btn--sm" to={`/shop/${reward.id}`}>
            {t("rewardCard.redeem")}
          </Link>
        </div>
      ) : (
        <div className="card-actions">
          <Link className="btn btn--outline btn--sm" to="/login">
            {t("rewardCard.loginToRedeem")}
          </Link>
        </div>
      )}
    </div>
  );
}
