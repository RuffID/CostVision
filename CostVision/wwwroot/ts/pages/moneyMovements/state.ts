import type { BankStatementImportBankDto, BankStatementImportLineErrorDto, BankStatementImportPreviewRowDto, MoneyMovementDto, UserAccountViewModel } from "./types.js";

export interface MoneyMovementsState {
    accounts: UserAccountViewModel[];
    movements: MoneyMovementDto[];
    selectedAccountId: string;
    dateFrom: string;
    dateTo: string;
    importBanks: BankStatementImportBankDto[];
    importRows: BankStatementImportPreviewRowDto[];
    importErrors: BankStatementImportLineErrorDto[];
}

export const state: MoneyMovementsState = {
    accounts: [],
    movements: [],
    selectedAccountId: "",
    dateFrom: "",
    dateTo: "",
    importBanks: [],
    importRows: [],
    importErrors: []
};
