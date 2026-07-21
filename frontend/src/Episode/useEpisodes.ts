import { useContext, useEffect, useMemo } from 'react';
import { EpisodeFile } from 'EpisodeFile/EpisodeFile';
import { EpisodeFileContext } from 'EpisodeFile/EpisodeFileProvider';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import Episode from './Episode';
import { useEpisodeOptions } from './episodeOptionsStore';
import { setEpisodeQueryKey } from './useEpisode';

const DEFAULT_EPISODES: Episode[] = [];

interface SeriesEpisodes {
  seriesId: number;
}

interface SeasonEpisodes {
  seriesId: number | undefined;
  seasonNumber: number | undefined;
  isSelection: boolean;
}

interface EpisodeIds {
  episodeIds: number[];
}

interface EpisodeFileId {
  episodeFileId: number;
}

export type EpisodeFilter =
  | SeriesEpisodes
  | SeasonEpisodes
  | EpisodeIds
  | EpisodeFileId;

const useEpisodes = (params: EpisodeFilter) => {
  const setQueryKey = !('isSelection' in params);

  const { isPlaceholderData, queryKey, ...result } = useApiQuery<Episode[]>({
    path: '/episode',
    queryParams:
      'isSelection' in params
        ? {
            seriesId: params.seriesId,
            seasonNumber: params.seasonNumber,
          }
        : { ...params },
    queryOptions: {
      enabled:
        ('seriesId' in params && params.seriesId !== undefined) ||
        ('episodeIds' in params && params.episodeIds?.length > 0) ||
        ('episodeFileId' in params && params.episodeFileId !== undefined),
    },
  });

  useEffect(() => {
    if (setQueryKey && !isPlaceholderData) {
      setEpisodeQueryKey('episodes', queryKey);
    }
  }, [setQueryKey, isPlaceholderData, queryKey]);

  return {
    ...result,
    queryKey,
    data: result.data ?? DEFAULT_EPISODES,
  };
};

export default useEpisodes;

export const useSeasonEpisodes = (seriesId: number, seasonNumber: number) => {
  const { data, ...result } = useEpisodes({ seriesId });
  const { sortKey, sortDirection } = useEpisodeOptions();
  const episodeFiles = useContext(EpisodeFileContext);

  const seasonEpisodes = useMemo(() => {
    // The path, size and custom format columns are backed by the episode's file,
    // not the episode itself, so they need predicates to be sortable at all.
    const getEpisodeFile = (episode: Episode): EpisodeFile | undefined =>
      episode.episodeFileId
        ? episodeFiles?.find((file) => file.id === episode.episodeFileId)
        : undefined;

    const sortPredicates = {
      path: (episode: Episode) => getEpisodeFile(episode)?.path ?? '',
      relativePath: (episode: Episode) =>
        getEpisodeFile(episode)?.relativePath ?? '',
      size: (episode: Episode) => getEpisodeFile(episode)?.size ?? 0,
      customFormatScore: (episode: Episode) =>
        getEpisodeFile(episode)?.customFormatScore ?? 0,
    };

    const { data: seasonEpisodes } = clientSideFilterAndSort<
      Episode,
      null,
      typeof sortPredicates
    >(
      data.filter((episode) => episode.seasonNumber === seasonNumber),
      {
        sortKey,
        sortDirection,
        // Always fall back to episode number so episodes never end up in the
        // order the API happened to return them in.
        secondarySortKey: 'episodeNumber',
        secondarySortDirection: sortDirection,
        sortPredicates,
      }
    );

    return seasonEpisodes;
  }, [data, episodeFiles, seasonNumber, sortKey, sortDirection]);

  return {
    ...result,
    data: seasonEpisodes,
  };
};
