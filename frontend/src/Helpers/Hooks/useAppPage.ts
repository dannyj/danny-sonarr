import { useEffect, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import { useTranslations } from 'App/useTranslations';
import useCommands from 'Commands/useCommands';
import useCustomFilters from 'Filters/useCustomFilters';
import { useInitializeLanguage } from 'Language/useLanguageName';
import { useLanguages } from 'Language/useLanguages';
import useSeries from 'Series/useSeries';
import { useQualityProfiles } from 'Settings/Profiles/Quality/useQualityProfiles';
import { useUiSettings } from 'Settings/UI/useUiSettings';
import { fetchCustomFilters } from 'Store/Actions/customFilterActions';
import {
  fetchImportLists,
  fetchIndexerFlags,
} from 'Store/Actions/settingsActions';
import useSystemStatus from 'System/Status/useSystemStatus';
import useTags from 'Tags/useTags';
import { ApiError } from 'Utilities/Fetch/fetchJson';

const createErrorsSelector = ({
  customFiltersError,
  systemStatusError,
  tagsError,
  translationsError,
  uiSettingsError,
  seriesError,
  qualityProfilesError,
  languagesError,
}: {
  customFiltersError: ApiError | null;
  systemStatusError: ApiError | null;
  tagsError: ApiError | null;
  translationsError: ApiError | null;
  uiSettingsError: ApiError | null;
  seriesError: ApiError | null;
  qualityProfilesError: ApiError | null;
  languagesError: ApiError | null;
}) =>
  createSelector(
    (state: AppState) => state.settings.importLists.error,
    (state: AppState) => state.settings.indexerFlags.error,
    (importListsError, indexerFlagsError) => {
      const hasError = !!(seriesError || uiSettingsError || systemStatusError);

      return {
        hasError,
        errors: {
          seriesError,
          customFiltersError,
          tagsError,
          uiSettingsError,
          qualityProfilesError,
          languagesError,
          importListsError,
          indexerFlagsError,
          systemStatusError,
          translationsError,
        },
      };
    }
  );

const useAppPage = () => {
  const dispatch = useDispatch();

  useCommands();
  useInitializeLanguage();

  const { error: customFiltersError } = useCustomFilters();

  const { isFetched: isSeriesFetched, error: seriesError } = useSeries();

  const { isFetched: isSystemStatusFetched, error: systemStatusError } =
    useSystemStatus();

  const { error: tagsError } = useTags();

  const { error: translationsError } = useTranslations();

  const { isFetched: isUiSettingsFetched, error: uiSettingsError } =
    useUiSettings();

  const { error: qualityProfilesError } = useQualityProfiles();

  const { error: languagesError } = useLanguages();

  const isPopulated =
    isSeriesFetched &&
    isSystemStatusFetched &&
    isUiSettingsFetched;

  const { hasError, errors } = useSelector(
    createErrorsSelector({
      customFiltersError,
      seriesError,
      systemStatusError,
      tagsError,
      translationsError,
      uiSettingsError,
      qualityProfilesError,
      languagesError,
    })
  );

  const isLocalStorageSupported = useMemo(() => {
    const key = 'sonarrTest';

    try {
      localStorage.setItem(key, key);
      localStorage.removeItem(key);

      return true;
    } catch {
      return false;
    }
  }, []);

  useEffect(() => {
    dispatch(fetchCustomFilters());
    dispatch(fetchImportLists());
    dispatch(fetchIndexerFlags());
  }, [dispatch]);

  return useMemo(() => {
    return { errors, hasError, isLocalStorageSupported, isPopulated };
  }, [errors, hasError, isLocalStorageSupported, isPopulated]);
};

export default useAppPage;
