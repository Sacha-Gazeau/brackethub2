import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { supabase } from "../lib/supabaseClient";

type UserMetadata = Record<string, string | null | undefined>;

type UserIdentity = {
  id?: string;
  provider?: string;
  identity_data?: Record<string, string | null | undefined> | null;
};

export default function LoginBtn() {
  const { t } = useTranslation();
  const apiUrl = import.meta.env.VITE_API_URL;

  // Après le redirect OAuth, on synchronise le profil via l'API backend
  useEffect(() => {
    const syncPublicUser = async () => {
      const {
        data: { session },
        error: sessionError,
      } = await supabase.auth.getSession();

      if (sessionError) {
        console.error("GET SESSION ERROR:", sessionError);
        return;
      }

      if (!session) {
        return;
      }

      if (!apiUrl) {
        console.error("VITE_API_URL is missing, profile sync skipped.");
        return;
      }

      const user = session.user;
      const meta = (user.user_metadata ?? {}) as UserMetadata;
      const identity = user.identities?.find(
        (item) => item.provider === "discord",
      ) as UserIdentity | undefined;

      const username =
        meta.user_name ||
        meta.preferred_username ||
        meta.full_name ||
        meta.name ||
        meta.username ||
        null;

      const avatar = meta.avatar_url || meta.picture || null;
      const identityData = identity?.identity_data ?? {};
      const discord_id =
        meta.provider_id ||
        meta.sub ||
        identityData.provider_id ||
        identityData.sub ||
        identityData.user_id ||
        (identity?.provider === "discord" ? identity?.id : null) ||
        null;

      try {
        const response = await fetch(`${apiUrl}/api/profiles/sync`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            id: user.id,
            email: user.email ?? null,
            username,
            avatar,
            discordId: discord_id,
          }),
        });

        if (!response.ok) {
          const payload = await response.json().catch(() => null);
          console.error("PROFILE SYNC ERROR:", payload?.message ?? response.statusText);
        }
      } catch (error) {
        console.error("PROFILE SYNC REQUEST ERROR:", error);
      }
    };

    void syncPublicUser();

    const { data: sub } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session?.user) {
        void syncPublicUser();
      }
    });

    return () => {
      sub.subscription.unsubscribe();
    };
  }, [apiUrl]);

  const loginWithDiscord = async () => {
    const { error } = await supabase.auth.signInWithOAuth({
      provider: "discord",
      options: {
        redirectTo: window.location.origin,
      },
    });

    if (error) console.log(error.message);
  };

  return (
    <button
      onClick={loginWithDiscord}
      className="btn btn--primary btn--full login-card__button"
    >
      <span className="login-card__dot" />
      {t("loginPage.button")}
    </button>
  );
}
