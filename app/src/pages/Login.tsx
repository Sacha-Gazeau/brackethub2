import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import LoginBtn from "../action/loginButton";

export default function Login() {
  const { t } = useTranslation();

  return (
    <main className="login-page">
      <div className="login-brand logo">
        <img src="/logo.webp" alt={t("common.logoAlt")} width="822" height="315" />
        <h1>{t("navigation.title")}</h1>
      </div>

      <div className="login-card">
        <h1 className="login-card__title">{t("loginPage.title")}</h1>
        <p className="login-card__subtitle">{t("loginPage.subtitle")}</p>

        <img src="discord.webp" className="login-card__icon" alt="" width="50" height="50" />

        <LoginBtn />
        <div className="login-card__divider" />

        <p className="login-card__small">{t("loginPage.info")}</p>
        <p className="login-card__small login-card__small--muted">
          {t("loginPage.warning")}
        </p>
      </div>

      <p className="login-note">{t("loginPage.warning2")}</p>

      <Link className="btn btn--ghost btn--sm login-back" to="/">
        ← {t("loginPage.home")}
      </Link>
    </main>
  );
}
