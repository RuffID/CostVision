import type { BankStatementImportBankDto, BankStatementImportLineErrorDto, BankStatementImportPreviewRowDto, MoneyMovementDto, UserAccountViewModel } from "./types.js";

export interface MoneyMovementsState {
    accounts: UserAccountViewModel[];
    movements: MoneyMovementDto[];
    selectedAccountId: string;
    dateFrom: string;
    dateTo: string;
    searchQuery: string;
    importBanks: BankStatementImportBankDto[];
    importRows: BankStatementImportPreviewRowDto[];
    importErrors: BankStatementImportLineErrorDto[];
    hideDuplicateImportRows: boolean;
}

export const state: MoneyMovementsState = {
    accounts: [],
    movements: [],
    selectedAccountId: "",
    dateFrom: "",
    dateTo: "",
    searchQuery: "",
    importBanks: [],
    importRows: [],
    importErrors: [],
    hideDuplicateImportRows: false
};
