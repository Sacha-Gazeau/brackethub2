import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { supabase } from "../lib/supabaseClient";
import { useEffect, useState } from "react";

export function Header() {
  const { t } = useTranslation();
  const [isLogin, setIsLogin] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  useEffect(() => {
    const getSession = async () => {
      const { data } = await supabase.auth.getSession();
      setIsLogin(!!data.session);
    };

    getSession();

    const { data: listener } = supabase.auth.onAuthStateChange(
      (_event, session) => {
        setIsLogin(!!session);
      },
    );

    return () => {
      listener.subscription.unsubscribe();
    };
  }, []);

  useEffect(() => {
    const handleResize = () => {
      if (window.innerWidth > 900) {
        setIsMenuOpen(false);
      }
    };

    window.addEventListener("resize", handleResize);

    return () => {
      window.removeEventListener("resize", handleResize);
    };
  }, []);

  const handleLogout = async () => {
    await supabase.auth.signOut();
    window.location.href = "/";
  };

  const closeMenu = () => {
    setIsMenuOpen(false);
  };

  const navId = "site-navigation";
  if (isLogin) {
    return (
      <header className="header">
        <div className="header__width">
          <Link to="/" className="logo" aria-label={t("navigation.title")}>
            <img src="/logo.webp" alt={t("common.logoAlt")} width="822" height="315" />
            <span className="logo__wordmark">{t("navigation.title")}</span>
          </Link>

          <button
            type="button"
            className={`nav-toggle ${isMenuOpen ? "nav-toggle--open" : ""}`}
            aria-expanded={isMenuOpen}
            aria-controls={navId}
            aria-label={
              isMenuOpen ? "Close navigation menu" : "Open navigation menu"
            }
            onClick={() => setIsMenuOpen((current) => !current)}
          >
            <span />
            <span />
            <span />
          </button>

          <nav id={navId} className={`nav ${isMenuOpen ? "nav--open" : ""}`}>
            <Link to="/" onClick={closeMenu}>
              {t("navigation.home")}
            </Link>
            <Link to="/tournaments" onClick={closeMenu}>
              {t("navigation.tournaments")}
            </Link>
            <Link to="/shop" onClick={closeMenu}>
              {t("navigation.shop")}
            </Link>
            <Link to="/profile" onClick={closeMenu}>
              {t("navigation.profile")}
            </Link>
            <Link to="/admin/test-notifications" onClick={closeMenu}>
              Test DM
            </Link>
            <button
              onClick={handleLogout}
              className="btn btn--secondary btn--sm"
            >
              {t("navigation.logout")}
            </button>
          </nav>
        </div>
      </header>
    );
  } else {
    return (
      <header className="header">
        <div className="header__width">
          <Link to="/" className="logo" aria-label={t("navigation.title")}>
            <img src="/logo.webp" alt={t("common.logoAlt")} width="822" height="315" />
            <span className="logo__wordmark">{t("navigation.title")}</span>
          </Link>

          <button
            type="button"
            className={`nav-toggle ${isMenuOpen ? "nav-toggle--open" : ""}`}
            aria-expanded={isMenuOpen}
            aria-controls={navId}
            aria-label={
              isMenuOpen ? "Close navigation menu" : "Open navigation menu"
            }
            onClick={() => setIsMenuOpen((current) => !current)}
          >
            <span />
            <span />
            <span />
          </button>

          <nav id={navId} className={`nav ${isMenuOpen ? "nav--open" : ""}`}>
            <Link to="/" onClick={closeMenu}>
              {t("navigation.home")}
            </Link>
            <Link to="/tournaments" onClick={closeMenu}>
              {t("navigation.tournaments")}
            </Link>
            <Link
              to="/login"
              onClick={closeMenu}
              className="btn btn--primary btn--sm login-btn"
            >
              {t("homePage.hero.loginWithDiscord")}
            </Link>
          </nav>
        </div>
      </header>
    );
  }
}
