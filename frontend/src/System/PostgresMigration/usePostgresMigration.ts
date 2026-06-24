import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import {
  PostgresMigrationConnection,
  PostgresMigrationStatus,
  PostgresMigrationValidation,
} from 'typings/PostgresMigration';

export function usePostgresMigrationStatus() {
  const result = useApiQuery<PostgresMigrationStatus>({
    path: '/system/postgres-migration/status',
    queryOptions: {
      refetchInterval: (query) => {
        const status = query.state.data;

        if (!status) {
          return 5000;
        }

        return status.state === 'Idle' || status.state === 'Succeeded'
          ? 15000
          : 3000;
      },
    },
  });

  return {
    ...result,
    data: result.data ?? {
      state: 'Idle',
      step: '',
      message: '',
      error: '',
      currentDatabaseType: '',
      restartPending: false,
      restartRequired: false,
      warnings: [],
    },
  };
}

export function useValidatePostgresMigration() {
  return useApiMutation<
    PostgresMigrationValidation,
    PostgresMigrationConnection
  >({
    path: '/system/postgres-migration/validate',
    method: 'POST',
  });
}

export function useStartPostgresMigration() {
  const queryClient = useQueryClient();

  return useApiMutation<PostgresMigrationStatus, PostgresMigrationConnection>({
    path: '/system/postgres-migration/start',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: ['/system/postgres-migration/status'],
        });
      },
    },
  });
}
