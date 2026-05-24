import { requireElementById, requireInputById, requireSelectById, requireTextAreaById } from "../../shared/dom.js";

export interface MoneyMovementsUi {
    alert: HTMLElement;
    createForm: HTMLFormElement;
    createButton: HTMLButtonElement;
    accountSelect: HTMLSelectElement;
    amountInput: HTMLInputElement;
    typeSelect: HTMLSelectElement;
    occurredAtInput: HTMLInputElement;
    commentInput: HTMLTextAreaElement;
    filterAccountSelect: HTMLSelectElement;
    dateFromInput: HTMLInputElement;
    dateToInput: HTMLInputElement;
    applyFilterButton: HTMLButtonElement;
    list: HTMLElement;
    count: HTMLElement;
    incomeSum: HTMLElement;
    expenseSum: HTMLElement;
    moveAccountModal: HTMLElement;
    moveAccountAlert: HTMLElement;
    moveSourceAccount: HTMLElement;
    moveTargetAccountSelect: HTMLSelectElement;
    confirmMoveAccountButton: HTMLButtonElement;
    removeFromAccountButton: HTMLButtonElement;
    deleteModal: HTMLElement;
    deleteInfo: HTMLElement;
    confirmDeleteButton: HTMLButtonElement;
}

export function getMoneyMovementsUi(): MoneyMovementsUi {
    return {
        alert: requireElementById("moneyMovementAlert"),
        createForm: requireElementById<HTMLFormElement>("moneyMovementCreateForm"),
        createButton: requireElementById<HTMLButtonElement>("moneyMovementCreateButton"),
        accountSelect: requireSelectById("moneyMovementAccount"),
        amountInput: requireInputById("moneyMovementAmount"),
        typeSelect: requireSelectById("moneyMovementType"),
        occurredAtInput: requireInputById("moneyMovementOccurredAt"),
        commentInput: requireTextAreaById("moneyMovementComment"),
        filterAccountSelect: requireSelectById("moneyMovementFilterAccount"),
        dateFromInput: requireInputById("moneyMovementDateFrom"),
        dateToInput: requireInputById("moneyMovementDateTo"),
        applyFilterButton: requireElementById<HTMLButtonElement>("moneyMovementApplyFilter"),
        list: requireElementById("moneyMovementList"),
        count: requireElementById("moneyMovementCount"),
        incomeSum: requireElementById("moneyMovementIncomeSum"),
        expenseSum: requireElementById("moneyMovementExpenseSum"),
        moveAccountModal: requireElementById("moveMoneyMovementAccountModal"),
        moveAccountAlert: requireElementById("moveMoneyMovementAccountAlert"),
        moveSourceAccount: requireElementById("moveMoneyMovementSourceAccount"),
        moveTargetAccountSelect: requireSelectById("moveMoneyMovementTargetAccount"),
        confirmMoveAccountButton: requireElementById<HTMLButtonElement>("confirmMoveMoneyMovementAccountButton"),
        removeFromAccountButton: requireElementById<HTMLButtonElement>("removeMoneyMovementFromAccountButton"),
        deleteModal: requireElementById("deleteMoneyMovementModal"),
        deleteInfo: requireElementById("deleteMoneyMovementInfo"),
        confirmDeleteButton: requireElementById<HTMLButtonElement>("confirmDeleteMoneyMovementButton")
    };
}
