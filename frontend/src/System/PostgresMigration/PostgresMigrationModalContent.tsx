import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import {
  PostgresMigrationConnection,
  PostgresMigrationStatus,
  PostgresMigrationValidation,
} from 'typings/PostgresMigration';
import { ApiError } from 'Utilities/Fetch/fetchJson';
import {
  useStartPostgresMigration,
  useValidatePostgresMigration,
} from './usePostgresMigration';
import styles from './PostgresMigrationModalContent.css';

export interface PostgresMigrationModalContentProps {
  status: PostgresMigrationStatus;
  onModalClose: () => void;
}

const defaultConnection: PostgresMigrationConnection = {
  host: '',
  port: 5432,
  user: '',
  password: '',
  mainDb: 'sonarr-main',
  logDb: 'sonarr-log',
};

function getErrorMessage(error?: ApiError | null) {
  return error?.statusBody?.message ?? error?.statusText ?? 'Request failed';
}

function PostgresMigrationModalContent({
  status,
  onModalClose,
}: PostgresMigrationModalContentProps) {
  const [connection, setConnection] =
    useState<PostgresMigrationConnection>(defaultConnection);
  const [validationResult, setValidationResult] =
    useState<PostgresMigrationValidation | null>(null);

  const validateMutation = useValidatePostgresMigration();
  const startMutation = useStartPostgresMigration();

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string>) => {
      setConnection((current) => ({
        ...current,
        [name]: name === 'port' ? Number(value) : value,
      }));
    },
    []
  );

  const handleValidate = useCallback(() => {
    validateMutation.mutate(connection, {
      onSuccess: (result) => {
        setValidationResult(result);
      },
      onError: () => {
        setValidationResult(null);
      },
    });
  }, [connection, validateMutation]);

  const handleStart = useCallback(() => {
    startMutation.mutate(connection);
  }, [connection, startMutation]);

  useEffect(() => {
    if (startMutation.isSuccess) {
      setValidationResult(null);
    }
  }, [startMutation.isSuccess]);

  const isBusy = validateMutation.isPending || startMutation.isPending;
  const canStart = useMemo(() => {
    return (
      validationResult?.isValid === true &&
      status.currentDatabaseType === 'SQLite' &&
      !isBusy
    );
  }, [validationResult, status.currentDatabaseType, isBusy]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>Migrate SQLite To Postgres</ModalHeader>

      <ModalBody className={styles.modalBody}>
        {status.currentDatabaseType !== 'SQLite' ? (
          <Alert kind={kinds.INFO}>
            Sonarr is already running on {status.currentDatabaseType}. This
            migration flow is only available when the current database is
            SQLite.
          </Alert>
        ) : null}

        <div className={styles.fieldGrid}>
          <div className={styles.field}>
            <label htmlFor="host">Host</label>
            <TextInput
              name="host"
              value={connection.host}
              onChange={handleInputChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="port">Port</label>
            <TextInput
              type="number"
              name="port"
              value={connection.port}
              onChange={handleInputChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="user">User</label>
            <TextInput
              name="user"
              value={connection.user}
              onChange={handleInputChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="password">Password</label>
            <TextInput
              type="password"
              name="password"
              value={connection.password}
              onChange={handleInputChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="mainDb">Main Database</label>
            <TextInput
              name="mainDb"
              value={connection.mainDb}
              onChange={handleInputChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="logDb">Log Database</label>
            <TextInput
              name="logDb"
              value={connection.logDb}
              onChange={handleInputChange}
            />
          </div>
        </div>

        <div className={styles.notes}>
          Sonarr will take a SQLite snapshot, copy data into Postgres, update
          its database settings, and restart automatically. The original SQLite
          files are kept in place for recovery.
        </div>

        {validationResult ? (
          <Alert
            kind={validationResult.isValid ? kinds.SUCCESS : kinds.WARNING}
          >
            {validationResult.message}
          </Alert>
        ) : null}

        {validateMutation.error ? (
          <Alert kind={kinds.DANGER}>
            {getErrorMessage(validateMutation.error)}
          </Alert>
        ) : null}

        {startMutation.error ? (
          <Alert kind={kinds.DANGER}>
            {getErrorMessage(startMutation.error)}
          </Alert>
        ) : null}

        {validationResult?.warnings?.length ? (
          <Alert kind={kinds.WARNING}>
            <ul className={styles.warningList}>
              {validationResult.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          </Alert>
        ) : null}
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <Button onPress={onModalClose}>Cancel</Button>

        <SpinnerButton
          kind={kinds.DEFAULT}
          isSpinning={validateMutation.isPending}
          isDisabled={isBusy || status.currentDatabaseType !== 'SQLite'}
          onPress={handleValidate}
        >
          Test Connection
        </SpinnerButton>

        <SpinnerButton
          kind={kinds.WARNING}
          isSpinning={startMutation.isPending}
          isDisabled={!canStart}
          onPress={handleStart}
        >
          Start Migration
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default PostgresMigrationModalContent;
