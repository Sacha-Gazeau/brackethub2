import { startTransition, useCallback, useEffect, useState } from "react";
import { supabase } from "../lib/supabaseClient";
import { useTranslation } from "react-i18next";

type ClaimResponse = {
  message?: string;
  coins?: number;
  lastDailyClaim?: string;
  alreadyClaimed?: boolean;
};

type UseDailyRewardParams = {
  userId: string | null;
  apiUrl: string | undefined;
  initialCoins?: number;
};

function isSameUtcDay(a: Date, b: Date) {
  return (
    a.getUTCFullYear() === b.getUTCFullYear() &&
    a.getUTCMonth() === b.getUTCMonth() &&
    a.getUTCDate() === b.getUTCDate()
  );
}

function getNextUtcMidnight(now: Date) {
  return new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1),
  );
}

function formatMsToHms(ms: number) {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

async function parseJsonSafe<T>(response: Response): Promise<T | null> {
  const raw = await response.text();
  if (!raw) return null;

  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

export function useDailyReward({
  userId,
  apiUrl,
  initialCoins = 0,
}: UseDailyRewardParams) {
  const { t } = useTranslation();
  const [coins, setCoins] = useState(initialCoins);
  const [lastDailyClaimAt, setLastDailyClaimAt] = useState<Date | null>(null);
  const [countdown, setCountdown] = useState<string>("00:00:00");
  const [isClaiming, setIsClaiming] = useState(false);
  const [claimMessage, setClaimMessage] = useState<string | null>(null);

  const claimedToday =
    !!lastDailyClaimAt && isSameUtcDay(lastDailyClaimAt, new Date());
  const canClaimDaily = !claimedToday;

  const syncRewardState = useCallback(async () => {
    if (!userId) {
      setCoins(initialCoins);
      setLastDailyClaimAt(null);
      return;
    }

    const { data, error } = await supabase
      .from("profiles")
      .select("coins,last_daily_claim")
      .eq("id", userId)
      .maybeSingle();

    if (error) {
      console.error("PROFILE REWARD STATE ERROR:", error);
      return;
    }

    if (!data) {
      setCoins(initialCoins);
      setLastDailyClaimAt(null);
      return;
    }

    if (data?.coins != null) {
      setCoins(data.coins);
    }

    if (data?.last_daily_claim) {
      setLastDailyClaimAt(new Date(data.last_daily_claim));
      return;
    }

    setLastDailyClaimAt(null);
  }, [initialCoins, userId]);

  useEffect(() => {
    const loadCoins = async () => {
      await syncRewardState();
    };

    loadCoins();
  }, [syncRewardState]);

  useEffect(() => {
    if (!lastDailyClaimAt) {
      queueMicrotask(() => {
        startTransition(() => {
          setCountdown("00:00:00");
        });
      });
      return;
    }

    const updateCountdown = () => {
      const now = new Date();
      if (!isSameUtcDay(lastDailyClaimAt, now)) {
        setCountdown("00:00:00");
        return;
      }

      const nextMidnight = getNextUtcMidnight(now);
      setCountdown(formatMsToHms(nextMidnight.getTime() - now.getTime()));
    };

    queueMicrotask(updateCountdown);
    const timer = window.setInterval(updateCountdown, 1000);

    return () => {
      window.clearInterval(timer);
    };
  }, [lastDailyClaimAt]);

  const claimDaily = async () => {
    if (!userId) {
      setClaimMessage(t("dailyReward.messages.userNotConnected"));
      return;
    }

    if (!apiUrl) {
      setClaimMessage(t("dailyReward.messages.missingApiUrl"));
      return;
    }

    try {
      setIsClaiming(true);
      setClaimMessage(null);

      const response = await fetch(`${apiUrl}/api/DailyReward/claim`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ userId }),
      });

      const payload = await parseJsonSafe<ClaimResponse>(response);

      if (!response.ok) {
        if (payload?.lastDailyClaim) {
          setLastDailyClaimAt(new Date(payload.lastDailyClaim));
        } else {
          await syncRewardState();
        }

        setClaimMessage(
          payload?.message ??
            t("dailyReward.messages.claimFailed", { status: response.status }),
        );
        return;
      }

      if (typeof payload?.coins === "number") {
        setCoins(payload.coins);
      }
      if (payload?.lastDailyClaim) {
        setLastDailyClaimAt(new Date(payload.lastDailyClaim));
      }

      setClaimMessage(payload?.message ?? t("dailyReward.messages.claimed"));
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : t("dailyReward.messages.unknownNetworkError");
      setClaimMessage(
        t("dailyReward.messages.apiRequestFailed", { message }),
      );
    } finally {
      setIsClaiming(false);
    }
  };

  return {
    coins,
    countdown,
    isClaiming,
    claimMessage,
    canClaimDaily,
    claimDaily,
  };
}
