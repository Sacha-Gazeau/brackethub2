import { useTranslation } from "react-i18next";
import { startTransition, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  buildIgdbCoverUrl,
  searchGames,
  type GameSearchResult,
} from "../../action/games";
import {
  buildCreateTournamentPayload,
  getMinTeamOptions,
  getCreateTournamentAccessToken,
  initialCreateTournamentFormState,
  normalizeMinTeams,
  loadCreateTournamentOptions,
  TEAM_SIZE_OPTIONS,
  submitCreateTournament,
  validateCreateTournamentForm,
  type CreateTournamentFieldErrors,
  type CreateTournamentFormState,
  type FormatOption,
} from "../../action/createTournament";

export default function CreateTournament() {
  const { t } = useTranslation();
  const apiUrl = import.meta.env.VITE_API_URL;
  const navigate = useNavigate();

  const [formats, setFormats] = useState<FormatOption[]>([]);
  const [gameResults, setGameResults] = useState<GameSearchResult[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [loadingGames, setLoadingGames] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<CreateTournamentFieldErrors>(
    {},
  );
  const [formValues, setFormValues] = useState<CreateTournamentFormState>(
    initialCreateTournamentFormState,
  );

  useEffect(() => {
    const loadOptionsOnMount = async () => {
      const { formats: loadedFormats, error } = await loadCreateTournamentOptions(t);

      if (error) {
        setErrorMessage(error);
      } else {
        setFormats(loadedFormats);
      }

      setLoadingOptions(false);
    };

    loadOptionsOnMount();
  }, [t]);

  useEffect(() => {
    if (!apiUrl) {
      startTransition(() => {
        setGameResults([]);
      });
      return;
    }

    const trimmedQuery = formValues.gameSearch.trim();
    if (trimmedQuery.length < 2) {
      startTransition(() => {
        setGameResults([]);
      });
      return;
    }

    const timeoutId = window.setTimeout(async () => {
      startTransition(() => {
        setLoadingGames(true);
      });
      const { data, error } = await searchGames(apiUrl, trimmedQuery, t);

      if (error) {
        startTransition(() => {
          setErrorMessage(error);
          setGameResults([]);
          setLoadingGames(false);
        });
      } else {
        startTransition(() => {
          setGameResults(data ?? []);
          setLoadingGames(false);
        });
      }
    }, 300);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [apiUrl, formValues.gameSearch, t]);

  const resetForm = () => {
    setFormValues(initialCreateTournamentFormState);
    setFieldErrors({});
    setGameResults([]);
  };

  const applySelectedGame = (
    gameName: string,
    results: GameSearchResult[],
  ) => {
    const matchedGame = results.find(
      (game) => game.name.toLowerCase() === gameName.trim().toLowerCase(),
    );

    if (!matchedGame) {
      setField("gameIgdbId", "");
      setField("gameName", "");
      setField("gameCoverUrl", "");
      return;
    }

    setField("gameIgdbId", String(matchedGame.id));
    setField("gameName", matchedGame.name);
    setField(
      "gameCoverUrl",
      buildIgdbCoverUrl(matchedGame.cover?.image_id) ?? "",
    );
  };

  const setField = <K extends keyof CreateTournamentFormState>(
    field: K,
    value: CreateTournamentFormState[K],
  ) => {
    setFormValues((previous) => ({
      ...previous,
      [field]: value,
    }));

    if (field in fieldErrors) {
      setFieldErrors((previous) => {
        const updated = { ...previous };
        delete updated[field as keyof CreateTournamentFieldErrors];
        return updated;
      });
    }
  };

  const clearMessages = () => {
    setErrorMessage(null);
    setSuccessMessage(null);
  };

  const minTeamOptions = getMinTeamOptions(formValues.teamCount);

  const handleTeamCountChange = (nextTeamCount: string) => {
    setFormValues((previous) => ({
      ...previous,
      teamCount: nextTeamCount,
      minTeams: normalizeMinTeams(previous.minTeams, nextTeamCount),
    }));

    setFieldErrors((previous) => {
      const updated = { ...previous };
      delete updated.teamCount;
      delete updated.minTeams;
      return updated;
    });
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    clearMessages();

    if (!apiUrl) {
      setErrorMessage(t("createTournamentPage.messages.missingApiUrl"));
      return;
    }

    const validation = validateCreateTournamentForm(formValues, t);
    if (validation.message) {
      setFieldErrors(validation.fieldErrors);
      setErrorMessage(validation.message);
      return;
    }

    setFieldErrors({});
    setSubmitting(true);
    const { token, error: tokenError } = await getCreateTournamentAccessToken(t);
    if (tokenError || !token) {
      setSubmitting(false);
      setErrorMessage(tokenError);
      return;
    }

    const payload = buildCreateTournamentPayload(formValues);
    const result = await submitCreateTournament(apiUrl, token, payload, t);
    setSubmitting(false);

    if (!result.ok) {
      setFieldErrors(result.fieldErrors ?? {});
      setErrorMessage(result.message);
      return;
    }

    setSuccessMessage(result.message);
    resetForm();

    if (result.slug) {
      navigate(`/tournament/${result.slug}`);
    }
  };

  return (
    <div className="create-tournament-page">
      <div className="create-header">
        <h2>{t("createTournamentPage.title")}</h2>
        <p>{t("createTournamentPage.subtitle")}</p>
      </div>

      <form className="create-form" onSubmit={handleSubmit}>
        <div className="create-form__hero">
          <div>
            <p className="create-form__eyebrow">
              {t("createTournamentPage.summary.eyebrow")}
            </p>
            <h3 className="create-form__title">
              {t("createTournamentPage.summary.title")}
            </h3>
            <p className="create-form__subtitle">
              {t("createTournamentPage.summary.subtitle")}
            </p>
          </div>

          <div className="create-form__stats">
            <div className="create-stat">
              <span>{t("createTournamentPage.summary.maxTeamsLabel")}</span>
              <strong>64</strong>
            </div>
            <div className="create-stat">
              <span>{t("createTournamentPage.summary.allowedSizesLabel")}</span>
              <strong>2 / 4 / 8 / 16 / 32 / 64</strong>
            </div>
            <div className="create-stat">
              <span>{t("createTournamentPage.summary.privacyLabel")}</span>
              <strong>{t("createTournamentPage.summary.privacyValue")}</strong>
            </div>
          </div>
        </div>

        {loadingOptions && (
          <p className="required-note">{t("createTournamentPage.messages.loadingOptions")}</p>
        )}
        {errorMessage && (
          <p className="required-note required-note--error">{errorMessage}</p>
        )}
        {successMessage && (
          <p className="required-note required-note--success">
            {successMessage}
          </p>
        )}

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.general")}</h3>
            <p>{t("createTournamentPage.sections.generalDescription")}</p>
          </div>
        <div className="form-group">
          <label htmlFor="tournament-name">{t("createTournamentPage.fields.name")}</label>
          <input
            id="tournament-name"
            type="text"
            className={fieldErrors.name ? "input--error" : ""}
            placeholder={t("createTournamentPage.fields.namePlaceholder")}
            value={formValues.name}
            onChange={(event) => setField("name", event.target.value)}
            aria-invalid={Boolean(fieldErrors.name)}
          />
          {fieldErrors.name && (
            <p className="field-error-text">{fieldErrors.name}</p>
          )}
        </div>

        <div className="form-group">
          <label htmlFor="tournament-game">{t("createTournamentPage.fields.game")}</label>
          <input
            id="tournament-game"
            type="text"
            className={fieldErrors.gameSearch ? "input--error" : ""}
            placeholder={t("createTournamentPage.fields.gamePlaceholder")}
            value={formValues.gameSearch}
            list="tournament-game-options"
            onChange={(event) => {
              const nextValue = event.target.value;
              setField("gameSearch", nextValue);
              applySelectedGame(nextValue, gameResults);
            }}
            disabled={loadingOptions}
            aria-invalid={Boolean(fieldErrors.gameSearch)}
          />
          <datalist id="tournament-game-options">
            {gameResults.slice(0, 10).map((game) => (
              <option key={game.id} value={game.name} />
            ))}
          </datalist>
          {formValues.gameIgdbId && formValues.gameName && (
            <div className="game-search__selected">
              {formValues.gameCoverUrl && (
                <img
                  src={formValues.gameCoverUrl}
                  alt={formValues.gameName}
                  className="game-search__cover"
                  width="48"
                  height="64"
                />
              )}
              <span>{formValues.gameName}</span>
            </div>
          )}
          {loadingGames && (
            <p className="required-note">{t("createTournamentPage.messages.searchingGames")}</p>
          )}
          {!loadingGames &&
            formValues.gameSearch.trim().length >= 2 &&
            gameResults.length === 0 &&
            !errorMessage && (
              <p className="required-note">{t("createTournamentPage.messages.noGamesFound")}</p>
            )}
          {fieldErrors.gameSearch && (
            <p className="field-error-text">{fieldErrors.gameSearch}</p>
          )}
        </div>
        </div>

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.schedule")}</h3>
            <p>{t("createTournamentPage.sections.scheduleDescription")}</p>
          </div>
        <div className="form-row form-row--triple">
          <div className="form-group">
            <label htmlFor="tournament-start-date">{t("createTournamentPage.fields.startDate")}</label>
            <input
              id="tournament-start-date"
              type="date"
              className={fieldErrors.startDate ? "input--error" : ""}
              value={formValues.startDate}
              onChange={(event) => setField("startDate", event.target.value)}
              aria-invalid={Boolean(fieldErrors.startDate)}
            />
            {fieldErrors.startDate && (
              <p className="field-error-text">{fieldErrors.startDate}</p>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="tournament-end-date">{t("createTournamentPage.fields.endDate")}</label>
            <input
              id="tournament-end-date"
              type="date"
              className={fieldErrors.endDate ? "input--error" : ""}
              value={formValues.endDate}
              onChange={(event) => setField("endDate", event.target.value)}
              aria-invalid={Boolean(fieldErrors.endDate)}
            />
            {fieldErrors.endDate && (
              <p className="field-error-text">{fieldErrors.endDate}</p>
            )}
          </div>
          <div className="form-group">
            <label htmlFor="tournament-start-time">{t("createTournamentPage.fields.startTime")}</label>
            <input
              id="tournament-start-time"
              type="time"
              className={fieldErrors.startTime ? "input--error" : ""}
              value={formValues.startTime}
              onChange={(event) => setField("startTime", event.target.value)}
              aria-invalid={Boolean(fieldErrors.startTime)}
            />
            {fieldErrors.startTime && (
              <p className="field-error-text">{fieldErrors.startTime}</p>
            )}
          </div>
        </div>
        </div>

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.structure")}</h3>
            <p>{t("createTournamentPage.sections.structureDescription")}</p>
          </div>
        <div className="form-row form-row--triple">
          <div className="form-group">
            <label htmlFor="tournament-team-count">{t("createTournamentPage.fields.teamCount")}</label>
            <select
              id="tournament-team-count"
              className={fieldErrors.teamCount ? "select--error" : ""}
              value={formValues.teamCount}
              onChange={(event) => handleTeamCountChange(event.target.value)}
              aria-invalid={Boolean(fieldErrors.teamCount)}
            >
              <option value="">—</option>
              {TEAM_SIZE_OPTIONS.map((teamSize) => (
                <option key={teamSize} value={teamSize}>
                  {teamSize}
                </option>
              ))}
            </select>
            {fieldErrors.teamCount && (
              <p className="field-error-text">{fieldErrors.teamCount}</p>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="tournament-min-teams">{t("createTournamentPage.fields.minTeams")}</label>
            <select
              id="tournament-min-teams"
              className={fieldErrors.minTeams ? "select--error" : ""}
              value={formValues.minTeams}
              onChange={(event) => setField("minTeams", event.target.value)}
              aria-invalid={Boolean(fieldErrors.minTeams)}
              disabled={!formValues.teamCount}
            >
              <option value="">—</option>
              {minTeamOptions.map((teamSize) => (
                <option key={teamSize} value={teamSize}>
                  {teamSize}
                </option>
              ))}
            </select>
            {fieldErrors.minTeams && (
              <p className="field-error-text">{fieldErrors.minTeams}</p>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="tournament-players-per-team">{t("createTournamentPage.fields.playersPerTeam")}</label>
            <input
              id="tournament-players-per-team"
              type="number"
              min={1}
              className={fieldErrors.playersPerTeam ? "input--error" : ""}
              value={formValues.playersPerTeam}
              onChange={(event) =>
                setField("playersPerTeam", event.target.value)
              }
              aria-invalid={Boolean(fieldErrors.playersPerTeam)}
            />
            {fieldErrors.playersPerTeam && (
              <p className="field-error-text">{fieldErrors.playersPerTeam}</p>
            )}
          </div>
        </div>
        </div>

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.access")}</h3>
            <p>{t("createTournamentPage.sections.accessDescription")}</p>
          </div>
        <div className="form-group">
          <label>{t("createTournamentPage.fields.tournamentType")}</label>

          <div className="type-options">
            <div
              className={`type-card ${formValues.privacy === "friends" ? "type-card--active" : ""}`}
              onClick={() => setField("privacy", "friends")}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  setField("privacy", "friends");
                }
              }}
              role="button"
              tabIndex={0}
            >
              <h4>{t("createTournamentPage.types.friendsTitle")}</h4>
              <p>{t("createTournamentPage.types.friendsDescription")}</p>
            </div>

            <div
              className={`type-card ${formValues.privacy === "public" ? "type-card--active" : ""}`}
              onClick={() => setField("privacy", "public")}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  setField("privacy", "public");
                }
              }}
              role="button"
              tabIndex={0}
            >
              <h4>{t("createTournamentPage.types.publicTitle")}</h4>
              <p>{t("createTournamentPage.types.publicDescription")}</p>
            </div>

          </div>
        </div>
        </div>

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.formatting")}</h3>
            <p>{t("createTournamentPage.sections.formattingDescription")}</p>
          </div>
        <div className="form-row">
          <div className="form-group">
            <label htmlFor="tournament-format">{t("createTournamentPage.fields.format")}</label>
            <select
              id="tournament-format"
              className={fieldErrors.formatId ? "select--error" : ""}
              value={formValues.formatId}
              onChange={(event) => setField("formatId", event.target.value)}
              disabled={loadingOptions}
              aria-invalid={Boolean(fieldErrors.formatId)}
            >
              <option value="">—</option>
              {formats.map((format) => (
                <option key={format.id} value={format.id}>
                  {format.name}
                </option>
              ))}
            </select>
            {fieldErrors.formatId && (
              <p className="field-error-text">{fieldErrors.formatId}</p>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="tournament-final-format">{t("createTournamentPage.fields.finalFormat")}</label>
            <select
              id="tournament-final-format"
              value={formValues.finalFormatId}
              onChange={(event) =>
                setField("finalFormatId", event.target.value)
              }
              disabled={loadingOptions}
            >
              <option value="">—</option>
              {formats.map((format) => (
                <option key={format.id} value={format.id}>
                  {format.name}
                </option>
              ))}
            </select>
          </div>
        </div>
        </div>

        <div className="create-section">
          <div className="create-section__header">
            <h3>{t("createTournamentPage.sections.details")}</h3>
            <p>{t("createTournamentPage.sections.detailsDescription")}</p>
          </div>
        <div className="form-group">
          <label htmlFor="tournament-description">{t("createTournamentPage.fields.description")}</label>
          <textarea
            id="tournament-description"
            rows={5}
            placeholder={t(
              "createTournamentPage.fields.descriptionPlaceholder",
            )}
            value={formValues.description}
            onChange={(event) => setField("description", event.target.value)}
          />
        </div>
        </div>

        <div className="form-actions">
          <button
            type="button"
            className="btn btn--outline"
            onClick={resetForm}
            disabled={submitting}
          >
            {t("createTournamentPage.actions.cancel")}
          </button>
          <button
            type="submit"
            className="btn btn--primary"
            disabled={submitting}
          >
            {submitting
              ? t("createTournamentPage.actions.creating")
              : t("createTournamentPage.actions.submit")}
          </button>
        </div>

        <p className="required-note">
          {t("createTournamentPage.requiredNote")}
        </p>
      </form>
    </div>
  );
}
