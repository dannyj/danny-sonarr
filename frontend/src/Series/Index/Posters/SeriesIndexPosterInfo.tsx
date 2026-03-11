import React from 'react';
import HeartRating from 'Components/HeartRating';
import SeriesTagList from 'Components/SeriesTagList';
import useCountryName from 'Internationalization/useCountryName';
import Language from 'Language/Language';
import { Ratings } from 'Series/Series';
import { QualityProfileModel } from 'Settings/Profiles/Quality/useQualityProfiles';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './SeriesIndexPosterInfo.css';

interface SeriesIndexPosterInfoProps {
  originalCountry?: string;
  originalLanguage?: Language;
  network?: string;
  showQualityProfile: boolean;
  qualityProfile?: QualityProfileModel;
  previousAiring?: string;
  added?: string;
  seasonCount: number;
  path: string;
  sizeOnDisk?: number;
  watchedOnPlex?: boolean;
  viewsLast30Days?: number;
  lastViewedAt?: string;
  viewTrend?: 'up' | 'down' | 'flat' | 'none';
  ratings: Ratings;
  tags: number[];
  sortKey: string;
  showRelativeDates: boolean;
  shortDateFormat: string;
  longDateFormat: string;
  timeFormat: string;
  showTags: boolean;
}

function SeriesIndexPosterInfo(props: SeriesIndexPosterInfoProps) {
  const {
    originalCountry,
    originalLanguage,
    network,
    qualityProfile,
    showQualityProfile,
    previousAiring,
    added,
    seasonCount,
    path,
    sizeOnDisk = 0,
    watchedOnPlex = false,
    viewsLast30Days = 0,
    lastViewedAt,
    viewTrend = 'none',
    ratings,
    tags,
    sortKey,
    showRelativeDates,
    shortDateFormat,
    longDateFormat,
    timeFormat,
    showTags,
  } = props;

  const originalCountryName = useCountryName(originalCountry);

  if (sortKey === 'network' && network) {
    return (
      <div className={styles.info} title={translate('Network')}>
        {network}
      </div>
    );
  }

  if (sortKey === 'originalCountry' && !!originalCountryName) {
    return (
      <div className={styles.info} title={translate('OriginalCountry')}>
        {originalCountryName}
      </div>
    );
  }

  if (sortKey === 'originalLanguage' && !!originalLanguage?.name) {
    return (
      <div className={styles.info} title={translate('OriginalLanguage')}>
        {originalLanguage.name}
      </div>
    );
  }

  if (
    sortKey === 'qualityProfileId' &&
    !showQualityProfile &&
    !!qualityProfile?.name
  ) {
    return (
      <div className={styles.info} title={translate('QualityProfile')}>
        {qualityProfile.name}
      </div>
    );
  }

  if (sortKey === 'previousAiring' && previousAiring) {
    return (
      <div
        className={styles.info}
        title={`${translate('PreviousAiring')}: ${formatDateTime(
          previousAiring,
          longDateFormat,
          timeFormat
        )}`}
      >
        {getRelativeDate({
          date: previousAiring,
          shortDateFormat,
          showRelativeDates,
          timeFormat,
          timeForToday: true,
        })}
      </div>
    );
  }

  if (sortKey === 'added' && added) {
    const addedDate = getRelativeDate({
      date: added,
      shortDateFormat,
      showRelativeDates,
      timeFormat,
      timeForToday: false,
    });

    return (
      <div
        className={styles.info}
        title={formatDateTime(added, longDateFormat, timeFormat)}
      >
        {translate('Added')}: {addedDate}
      </div>
    );
  }

  if (sortKey === 'seasonCount') {
    let seasons = translate('OneSeason');

    if (seasonCount === 0) {
      seasons = translate('NoSeasons');
    } else if (seasonCount > 1) {
      seasons = translate('CountSeasons', { count: seasonCount });
    }

    return <div className={styles.info}>{seasons}</div>;
  }

  if (!showTags && sortKey === 'tags' && tags.length) {
    return (
      <div className={styles.tags}>
        <div className={styles.tagsList}>
          <SeriesTagList tags={tags} />
        </div>
      </div>
    );
  }

  if (sortKey === 'path') {
    return (
      <div className={styles.info} title={translate('Path')}>
        {path}
      </div>
    );
  }

  if (sortKey === 'sizeOnDisk') {
    return (
      <div className={styles.info} title={translate('SizeOnDisk')}>
        {formatBytes(sizeOnDisk)}
      </div>
    );
  }

  if (sortKey === 'watchedOnPlex') {
    return (
      <div className={styles.info} title={translate('WatchedOnPlex')}>
        {watchedOnPlex ? translate('Yes') : translate('No')}
      </div>
    );
  }

  if (sortKey === 'viewsLast30Days') {
    return (
      <div className={styles.info} title={translate('ViewsLast30Days')}>
        {translate('CountViewsLast30Days', { count: viewsLast30Days })}
      </div>
    );
  }

  if (sortKey === 'lastViewedAt' && lastViewedAt) {
    return (
      <div
        className={styles.info}
        title={formatDateTime(lastViewedAt, longDateFormat, timeFormat)}
      >
        {translate('LastViewed')}:{' '}
        {getRelativeDate({
          date: lastViewedAt,
          shortDateFormat,
          showRelativeDates,
          timeFormat,
          timeForToday: true,
        })}
      </div>
    );
  }

  if (sortKey === 'viewTrend') {
    return (
      <div className={styles.info} title={translate('PlexViewTrend')}>
        {translate(
          `PlexViewTrend${viewTrend[0].toUpperCase()}${viewTrend.slice(1)}`
        )}
      </div>
    );
  }

  if (sortKey === 'ratings' && ratings.value) {
    return (
      <div className={styles.info} title={translate('Rating')}>
        <HeartRating rating={ratings.value} votes={ratings.votes} />
      </div>
    );
  }

  return null;
}

export default SeriesIndexPosterInfo;
