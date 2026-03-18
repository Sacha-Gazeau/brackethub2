import { startTransition, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { supabase } from "../../lib/supabaseClient";
import TournamentAdminNav from "../../components/tournaments/admin/TournamentAdminNav";
import TournamentStageSummary from "../../components/tournaments/admin/TournamentStageSummary";
import TournamentBracketManualEditor from "../../components/tournaments/admin/TournamentBracketManualEditor";
import TournamentGroupsManualEditor from "../../components/tournaments/admin/TournamentGroupsManualEditor";
import TournamentMatchResultEditor from "../../components/tournaments/admin/TournamentMatchResultEditor";
import TournamentStagePreview from "../../components/tournaments/admin/TournamentStagePreview";
import { getCreateTournamentAccessToken } from "../../action/createTournament";
import {
  canLaunchTournamentStage,
  createAutomaticBracketSlots,
  createEmptyBracketSlots,
  createInitialGroups,
  getWinsNeededForMatch,
  isValidMatchScore,
  loadTournamentAdminStageData,
  saveBracketStage,
  saveGroupsStage,
  saveMatchResult,
  type BracketSlot,
  type ExistingGroup,
  type ExistingMatch,
  type GroupDefinition,
  type TournamentAdminStageSummary,
  type TournamentAdminTeam,
  type TournamentStageMode,
  type TournamentStageType,
} from "../../action/tournamentAdminStage";

export default function TournamentAdminStage() {
  const { t } = useTranslation();
  const { id: slug } = useParams<{ id: string }>();

  const [tournament, setTournament] = useState<TournamentAdminStageSummary | null>(null);
  const [acceptedTeams, setAcceptedTeams] = useState<TournamentAdminTeam[]>([]);
  const [existingPreviewGroups, setExistingPreviewGroups] = useState<ExistingGroup[]>([]);
  const [existingMatches, setExistingMatches] = useState<ExistingMatch[]>([]);
  const [selectedType, setSelectedType] = useState<TournamentStageType>("bracket");
  const [selectedMode, setSelectedMode] = useState<TournamentStageMode>("automatic");
  const [manualSlots, setManualSlots] = useState<BracketSlot[]>([]);
  const [manualGroups, setManualGroups] = useState<GroupDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [savingMatchId, setSavingMatchId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [formatName, setFormatName] = useState("BO1");
  const [finalFormatName, setFinalFormatName] = useState<string | null>(null);

  const resetManualBracket = (teamCount: number) => {
    setManualSlots(createEmptyBracketSlots(teamCount));
  };

  const resetManualGroups = (teamCount: number) => {
    setManualGroups(createInitialGroups(teamCount));
  };

  useEffect(() => {
    const loadPage = async () => {
      if (!slug) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.missingSlug"));
          setLoading(false);
        });
        return;
      }

      const {
        data: { session },
      } = await supabase.auth.getSession();

      if (!session?.user?.id) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.loginRequired"));
          setLoading(false);
        });
        return;
      }

      const result = await loadTournamentAdminStageData(slug, t);
      if (result.error || !result.tournament) {
        startTransition(() => {
          setError(result.error ?? t("tournamentAdmin.messages.loadFailed"));
          setLoading(false);
        });
        return;
      }

      if (result.tournament.user_id !== session.user.id) {
        startTransition(() => {
          setError(t("tournamentAdmin.messages.organizerOnly"));
          setLoading(false);
        });
        return;
      }

      startTransition(() => {
        setTournament(result.tournament);
        setAcceptedTeams(result.acceptedTeams);
        setExistingPreviewGroups(result.existingGroups);
        setExistingMatches(result.existingMatches);
        setLoading(false);
      });

      resetManualGroups(result.acceptedTeams.length);
      resetManualBracket(result.acceptedTeams.length);

      const formatIds = [
        result.tournament.format,
        result.tournament.final_format,
      ].filter((value): value is number => Boolean(value));

      if (formatIds.length > 0) {
        const { data: formatRows } = await supabase
          .from("formats")
          .select("id, name")
          .in("id", formatIds);

        const formatMap = new Map(
          (((formatRows as { id: number; name: string }[] | null) ?? [])).map((row) => [
            row.id,
            row.name,
          ]),
        );

        setFormatName(formatMap.get(result.tournament.format ?? -1) ?? "BO1");
        setFinalFormatName(
          result.tournament.final_format
            ? formatMap.get(result.tournament.final_format) ?? null
            : null,
        );
      } else {
        setFormatName("BO1");
        setFinalFormatName(null);
      }
    };

    void loadPage();
  }, [slug, t]);

  if (loading) {
    return <div className="tournament-admin-page"><p>{t("tournamentAdmin.loading")}</p></div>;
  }

  if (error || !tournament || !slug) {
    return (
      <div className="tournament-admin-page">
        <p>{error ?? t("tournamentAdmin.messages.tournamentNotFound")}</p>
        <Link className="btn btn--outline" to={slug ? `/tournament/${slug}` : "/tournaments"}>
          {t("tournamentAdmin.actions.backToTournament")}
        </Link>
      </div>
    );
  }

  const launchState = canLaunchTournamentStage(tournament, acceptedTeams.length);
  const maxRound = Math.max(...existingMatches.map((match) => match.roundNumber), 1);

  const handleTypeChange = (nextType: TournamentStageType) => {
    setSelectedType(nextType);

    if (selectedMode !== "manual") {
      return;
    }

    if (nextType === "bracket") {
      resetManualBracket(acceptedTeams.length);
      return;
    }

    resetManualGroups(acceptedTeams.length);
  };

  const handleModeChange = (nextMode: TournamentStageMode) => {
    setSelectedMode(nextMode);

    if (nextMode !== "manual") {
      return;
    }

    if (selectedType === "bracket") {
      resetManualBracket(acceptedTeams.length);
      return;
    }

    resetManualGroups(acceptedTeams.length);
  };

  const handleSave = async () => {
    if (!tournament) {
      return;
    }

    setSaving(true);
    setError(null);
    setFeedback(null);

    const result =
      selectedType === "bracket"
        ? await saveBracketStage({
            tournamentId: tournament.id,
            acceptedTeams,
            mode: selectedMode,
            manualSlots: selectedMode === "manual" ? manualSlots : createAutomaticBracketSlots(acceptedTeams),
            t,
          })
        : await saveGroupsStage({
            tournamentId: tournament.id,
            acceptedTeams,
            mode: selectedMode,
            manualGroups,
            t,
          });

    setSaving(false);

    if (!result.ok) {
      setError(result.message);
      return;
    }

    setFeedback(result.message);

    const refreshed = await loadTournamentAdminStageData(slug, t);
    if (!refreshed.error && refreshed.tournament) {
      setTournament(refreshed.tournament);
      setAcceptedTeams(refreshed.acceptedTeams);
      setExistingPreviewGroups(refreshed.existingGroups);
      setExistingMatches(refreshed.existingMatches);
    }
  };

  const handleSaveMatchResult = async (
    matchId: number,
    team1Score: number,
    team2Score: number,
  ) => {
    const apiUrl = import.meta.env.VITE_API_URL;
    if (!slug || !apiUrl) {
      setError(t("tournamentAdmin.messages.missingApiUrl"));
      return;
    }

    const match = existingMatches.find((item) => item.id === matchId);
    const activeFormatName =
      finalFormatName && (match?.roundNumber ?? 1) === maxRound
        ? finalFormatName
        : formatName;
    const winsNeeded = getWinsNeededForMatch(
      match?.roundNumber ?? 1,
      maxRound,
      formatName,
      finalFormatName,
    );

    if (!isValidMatchScore(team1Score, team2Score, winsNeeded)) {
      setError(`Ongeldige score voor ${activeFormatName}. Het eerste team dat ${winsNeeded} wint, wint de match.`);
      return;
    }

    const { token, error: tokenError } = await getCreateTournamentAccessToken(t);
    if (tokenError || !token) {
      setError(tokenError ?? t("tournamentAdmin.messages.connectionFailed"));
      return;
    }

    setSavingMatchId(matchId);
    setError(null);
    setFeedback(null);

    const result = await saveMatchResult(
      apiUrl,
      slug,
      matchId,
      team1Score,
      team2Score,
      token,
      t,
    );

    setSavingMatchId(null);

    if (!result.ok) {
      setError(result.message);
      return;
    }

    setFeedback(result.message);

    const refreshed = await loadTournamentAdminStageData(slug, t);
    if (!refreshed.error && refreshed.tournament) {
      setTournament(refreshed.tournament);
      setAcceptedTeams(refreshed.acceptedTeams);
      setExistingPreviewGroups(refreshed.existingGroups);
      setExistingMatches(refreshed.existingMatches);
    }
  };

  return (
    <div className="tournament-admin-page">
      <header className="tournament-admin-page__header">
        <div>
          <p className="tournament-admin-page__eyebrow">Toernooistructuur</p>
          <h1>{tournament.name}</h1>
          <p>Start hier de bracket of de groepsfase van dit toernooi.</p>
        </div>

        <Link className="btn btn--outline" to={`/tournament/${slug}/admin`}>
          Terug naar dashboard
        </Link>
      </header>

      <TournamentAdminNav slug={slug} active="stage" />

      <TournamentStageSummary
        tournament={tournament}
        acceptedTeamsCount={acceptedTeams.length}
        canLaunch={launchState.canLaunch}
      />

      {error && <p className="tournament-admin-page__feedback tournament-admin-page__feedback--error">{error}</p>}
      {feedback && <p className="tournament-admin-page__feedback tournament-admin-page__feedback--success">{feedback}</p>}

      {launchState.structureExists ? (
        <>
          <TournamentStagePreview
            type={tournament.tournament_type}
            groups={existingPreviewGroups}
            matches={existingMatches}
            teams={acceptedTeams}
          />

          {tournament.tournament_type === "bracket" && existingMatches.length > 0 && (
            <TournamentMatchResultEditor
              matches={existingMatches}
              teams={acceptedTeams}
              formatName={formatName}
              finalFormatName={finalFormatName}
              savingMatchId={savingMatchId}
              onSave={handleSaveMatchResult}
            />
          )}
        </>
      ) : (
        <section className="tournament-stage-builder">
          <div className="tournament-stage-builder__section">
            <h2>1. Kies het type structuur</h2>
            <div className="tournament-stage-builder__choices">
              <button
                type="button"
                className={`tournament-stage-builder__choice ${
                  selectedType === "bracket" ? "tournament-stage-builder__choice--active" : ""
                }`}
                onClick={() => handleTypeChange("bracket")}
              >
                <strong>Bracket maken</strong>
                <span>Genereer een rechtstreekse eliminatiebracket.</span>
              </button>

              <button
                type="button"
                className={`tournament-stage-builder__choice ${
                  selectedType === "groups_to_bracket"
                    ? "tournament-stage-builder__choice--active"
                    : ""
                }`}
                onClick={() => handleTypeChange("groups_to_bracket")}
              >
                <strong>Eerst poules, daarna bracket</strong>
                <span>Start met groepen en bereid later de eindbracket voor.</span>
              </button>
            </div>
          </div>

          <div className="tournament-stage-builder__section">
            <h2>2. Kies de manier van opbouwen</h2>
            <div className="tournament-stage-builder__choices tournament-stage-builder__choices--compact">
              <button
                type="button"
                className={`tournament-stage-builder__choice ${
                  selectedMode === "automatic" ? "tournament-stage-builder__choice--active" : ""
                }`}
                onClick={() => handleModeChange("automatic")}
              >
                <strong>Automatische generatie</strong>
                <span>Gebruik de geaccepteerde teams om de structuur automatisch op te bouwen.</span>
              </button>

              <button
                type="button"
                className={`tournament-stage-builder__choice ${
                  selectedMode === "manual" ? "tournament-stage-builder__choice--active" : ""
                }`}
                onClick={() => handleModeChange("manual")}
              >
                <strong>Handmatige opbouw</strong>
                <span>Plaats de teams zelf in groepen of bracket-slots.</span>
              </button>
            </div>
          </div>

          {!launchState.enoughTeams && (
            <p className="tournament-admin-page__feedback tournament-admin-page__feedback--error">
              Je hebt minstens {tournament.min_teams} geaccepteerde teams nodig om dit toernooi te starten.
            </p>
          )}

          {launchState.enoughTeams && selectedMode === "manual" && selectedType === "bracket" && (
            <TournamentBracketManualEditor
              slots={manualSlots}
              teams={acceptedTeams}
              onChange={setManualSlots}
            />
          )}

          {launchState.enoughTeams &&
            selectedMode === "manual" &&
            selectedType === "groups_to_bracket" && (
              <TournamentGroupsManualEditor
                teams={acceptedTeams}
                groups={manualGroups}
                onChange={setManualGroups}
              />
            )}

          {launchState.enoughTeams && selectedMode === "automatic" && (
            <div className="tournament-stage-builder__auto">
              <h3>Automatische generatie</h3>
              <p>
                {selectedType === "bracket"
                  ? "De geaccepteerde teams worden automatisch in de bracket geplaatst."
                  : "De geaccepteerde teams worden automatisch over de groepen verdeeld."}
              </p>
            </div>
          )}

          <div className="tournament-stage-builder__actions">
            <button
              type="button"
              className="btn btn--primary"
              disabled={!launchState.canLaunch || saving}
              onClick={handleSave}
            >
              {saving ? "Opslaan..." : "Toernooistructuur opslaan"}
            </button>
          </div>
        </section>
      )}
    </div>
  );
}
