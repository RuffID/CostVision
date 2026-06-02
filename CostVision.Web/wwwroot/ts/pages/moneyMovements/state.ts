import type { BankStatementImportBankDto, BankStatementImportLineErrorDto, BankStatementImportPreviewRowDto, MoneyMovementDto, MoneyMovementReceiptDto, PendingMoneyMovementAccountAction, UserAccountViewModel } from "./types.js";

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
    linkedReceipts: MoneyMovementReceiptDto[];
    receiptCandidates: MoneyMovementReceiptDto[];
    pendingAccountAction: PendingMoneyMovementAccountAction | null;
    selectedReceiptsMoneyMovementId: string | null;
    preserveMoveModalPendingOnHide: boolean;
    selectedImportFile: File | null;
    editingCommentMovementId: string | null;
    searchDebounceTimerId: number | null;
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
    hideDuplicateImportRows: false,
    linkedReceipts: [],
    receiptCandidates: [],
    pendingAccountAction: null,
    selectedReceiptsMoneyMovementId: null,
    preserveMoveModalPendingOnHide: false,
    selectedImportFile: null,
    editingCommentMovementId: null,
    searchDebounceTimerId: null
};
