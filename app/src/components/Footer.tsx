import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

export function Footer() {
  const { t } = useTranslation();

  return (
    <footer className="footer">
      <div className="footer__width">
        <div className="footer-container">
          <div className="footer-column">
            <h4>{t("navigation.title")}</h4>
            <p>{t("footer.platform")}</p>
          </div>

          <div className="footer-column">
            <h4>{t("footer.navigation")}</h4>
            <Link to="/">{t("navigation.home")}</Link>
            <Link to="/tournaments">{t("navigation.tournaments")}</Link>
          </div>

          <div className="footer-column">
            <h4>{t("footer.legal")}</h4>
            <Link to="/terms">{t("footer.terms")}</Link>
            <Link to="/privacy">{t("footer.privacy")}</Link>
          </div>

          <div className="footer-column">
            <h4>{t("footer.support")}</h4>
            <Link to="/contact">{t("footer.contact")}</Link>
            <Link to="/faq">{t("footer.faq")}</Link>
          </div>
        </div>

        <div className="footer-divider"></div>

        <div className="footer-bottom">
          © 2026 {t("navigation.title")}. {t("footer.rights")}
        </div>
      </div>
    </footer>
  );
}




