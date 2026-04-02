export interface PostgresMigrationConnection {
  host: string;
  port: number;
  user: string;
  password: string;
  mainDb: string;
  logDb: string;
}

export interface PostgresMigrationStatus {
  state: string;
  step: string;
  message: string;
  error: string;
  currentDatabaseType: string;
  restartPending: boolean;
  restartRequired: boolean;
  warnings: string[];
}

export interface PostgresMigrationValidation {
  isValid: boolean;
  message: string;
  warnings: string[];
}
