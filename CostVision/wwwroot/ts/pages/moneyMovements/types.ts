export const MONEY_MOVEMENT_TYPE_EXPENSE = 0;
export const MONEY_MOVEMENT_TYPE_INCOME = 1;

export type MoneyMovementType = typeof MONEY_MOVEMENT_TYPE_EXPENSE | typeof MONEY_MOVEMENT_TYPE_INCOME;

export interface UserAccountViewModel {
    id: string;
    name: string;
    description?: string | null;
    colorHex: string;
    isActive: boolean;
    canManage: boolean;
    ownerName: string;
    accessRole: number;
}

export interface MoneyMovementDto {
    id: string;
    accountId: string;
    accountName: string;
    accountColorHex: string;
    amount: number;
    type: MoneyMovementType;
    occurredAt: string;
    comment?: string | null;
    importComment?: string | null;
    performedByUserId: string;
    performedByUserName: string;
    source: number;
    linkedReceiptCount: number;
    availableReceiptCount: number;
    linkedReceiptsTotalSum: number;
}

export interface CreateMoneyMovementRequest {
    accountId: string;
    amount: number;
    type: MoneyMovementType | null;
    occurredAt: string;
    comment: string | null;
}

export interface MoveMoneyMovementToAccountRequest {
    moneyMovementId: string;
    sourceAccountId: string;
    targetAccountId: string;
}

export interface DeleteMoneyMovementRequest {
    moneyMovementId: string;
    accountId: string;
}

export interface UpdateMoneyMovementCommentRequest {
    moneyMovementId: string;
    accountId: string;
    comment: string | null;
}

export interface MoneyMovementReceiptDto {
    receiptId: string;
    dateTime: string;
    retailPlace: string;
    totalSum: number;
    accountName: string;
    isLinkedToOtherMoneyMovement: boolean;
}

export interface GetMoneyMovementReceiptCandidatesRequest {
    moneyMovementId: string;
    useTimeWindow: boolean;
    timeWindowHours: number | null;
    dateFrom: string | null;
    dateTo: string | null;
    useAmountFilter: boolean;
    amountTolerance: number | null;
    excludeLinkedReceipts: boolean;
}

export interface LinkMoneyMovementReceiptRequest {
    moneyMovementId: string;
    receiptId: string;
}

export interface UnlinkMoneyMovementReceiptRequest {
    moneyMovementId: string;
    receiptId: string;
}

export interface PendingMoneyMovementAccountAction {
    moneyMovementId: string;
    sourceAccountId: string;
}

export interface BankStatementImportBankDto {
    id: string;
    name: string;
    description: string;
}

export interface BankStatementImportPreviewDto {
    bankId: string;
    accountId: string;
    rows: BankStatementImportPreviewRowDto[];
    errors: BankStatementImportLineErrorDto[];
}

export interface BankStatementImportPreviewRowDto {
    clientRowId: string;
    occurredAt: string;
    processedAt: string;
    amount: number;
    type: MoneyMovementType;
    comment: string;
    importComment: string;
    isDuplicate: boolean;
    duplicateMoneyMovementId?: string | null;
    sourceLineNumber: number;
    replaceDuplicate?: boolean;
}

export interface BankStatementImportLineErrorDto {
    lineNumber: number;
    message: string;
    rawText: string;
}

export interface BankStatementImportRowRequest {
    occurredAt: string;
    amount: number;
    type: MoneyMovementType;
    comment: string | null;
    importComment: string;
    duplicateMoneyMovementId?: string | null;
    replaceDuplicate: boolean;
}

export interface SaveBankStatementImportRequest {
    accountId: string;
    rows: BankStatementImportRowRequest[];
}

export interface BankStatementImportResultDto {
    createdCount: number;
    updatedCount: number;
    errorCount: number;
    errors: BankStatementImportLineErrorDto[];
}

export interface ReceiptItemDto {
    name: string;
    quantity: number;
    price: number;
    sum: number;
}

export interface ReceiptAccountDto {
    id: string;
    receiptId: string;
    name: string;
    colorHex: string;
    canEditReceipt: boolean;
    accessRole?: number | null;
}

export interface ReceiptDto {
    id: string;
    dateTime: string;
    retailPlace: string;
    retailPlaceAddress: string;
    totalSum: number;
    fiscalDriveNumber: string;
    fiscalDocumentNumber: string;
    fiscalSign: string;
    accountId: string | null;
    accountName: string;
    moneyMovementCount: number;
    availableMoneyMovementCount: number;
    moneyMovementsTotalSum: number;
    accounts: ReceiptAccountDto[];
    items: ReceiptItemDto[];
}
