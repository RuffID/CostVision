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
