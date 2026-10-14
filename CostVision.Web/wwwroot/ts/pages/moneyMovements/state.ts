import { createRequestCoordinator } from "../../shared/requestCoordinator.js";
import type { MoneyMovementList } from "./types.js";
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

export function createMoneyMovementsState(): MoneyMovementsState {
    return {
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
}

export function createMoneyMovementsSession() {
    return {
        data: createMoneyMovementsState(),
        movementPage: 1,
        candidatePage: 1,
        movementFilterKey: "",
        candidateFilterKey: "",
        movementRequests: createRequestCoordinator(),
        initialRequests: createRequestCoordinator(),
        pageContext: createRequestCoordinator(),
        linkedRequests: createRequestCoordinator(),
        candidateRequests: createRequestCoordinator(),
        detailRequests: createRequestCoordinator(),
        receiptsRevision: 0,
        creating: false,
        commands: new Set<string>(),
        movementTotals: null as MoneyMovementList | null,
        forgeryToken: null as string | null,
    };
}

export type MoneyMovementsSession = ReturnType<typeof createMoneyMovementsSession>;
