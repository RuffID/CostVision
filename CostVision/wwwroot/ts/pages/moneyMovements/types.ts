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

export interface PendingMoneyMovementAccountAction {
    moneyMovementId: string;
    sourceAccountId: string;
}
