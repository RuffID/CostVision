import type { MoneyMovementDto, UserAccountViewModel } from "./types.js";

export interface MoneyMovementsState {
    accounts: UserAccountViewModel[];
    movements: MoneyMovementDto[];
    selectedAccountId: string;
    dateFrom: string;
    dateTo: string;
}

export const state: MoneyMovementsState = {
    accounts: [],
    movements: [],
    selectedAccountId: "",
    dateFrom: "",
    dateTo: ""
};
