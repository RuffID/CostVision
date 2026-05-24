import { buildFormHeaders, buildJsonHeaders, sendJsonRequest, unwrapServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import type { BankStatementImportBankDto, BankStatementImportPreviewDto, CreateMoneyMovementRequest, DeleteMoneyMovementRequest, MoneyMovementDto, MoveMoneyMovementToAccountRequest, SaveBankStatementImportRequest, UserAccountViewModel } from "./types.js";

type AccountListResponse = ServiceResultWithData<UserAccountViewModel[]>;
type ImportBankListResponse = ServiceResultWithData<BankStatementImportBankDto[]>;
type ImportPreviewResponse = ServiceResultWithData<BankStatementImportPreviewDto>;
type MoneyMovementListResponse = ServiceResultWithData<MoneyMovementDto[]>;
type MoneyMovementResponse = ServiceResultWithData<MoneyMovementDto>;
type BooleanResponse = ServiceResultWithData<boolean>;

export async function loadAccounts(forgeryToken: string | null): Promise<UserAccountViewModel[]> {
    const response = await sendJsonRequest<AccountListResponse>("?handler=Accounts", "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<UserAccountViewModel[]>(response);
}

export async function loadImportBanks(forgeryToken: string | null): Promise<BankStatementImportBankDto[]> {
    const response = await sendJsonRequest<ImportBankListResponse>("?handler=ImportBanks", "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<BankStatementImportBankDto[]>(response);
}

export async function loadMoneyMovements(forgeryToken: string | null, dateFrom: string, dateTo: string, accountId: string): Promise<MoneyMovementDto[]> {
    const params = new URLSearchParams();
    params.set("dateFrom", dateFrom);
    params.set("dateTo", dateTo);

    if (accountId) {
        params.set("accountId", accountId);
    }

    const response = await sendJsonRequest<MoneyMovementListResponse>(`?handler=List&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<MoneyMovementDto[]>(response);
}

export async function createMoneyMovement(forgeryToken: string | null, request: CreateMoneyMovementRequest): Promise<MoneyMovementDto> {
    const response = await sendJsonRequest<MoneyMovementResponse>("?handler=Create", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<MoneyMovementDto>(response);
}

export async function moveMoneyMovementToAccount(forgeryToken: string | null, request: MoveMoneyMovementToAccountRequest): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>("?handler=MoveToAccount", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<boolean>(response);
}

export async function deleteMoneyMovement(forgeryToken: string | null, request: DeleteMoneyMovementRequest): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>("?handler=Delete", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<boolean>(response);
}

export async function previewBankStatementImport(forgeryToken: string | null, bankId: string, accountId: string, file: File): Promise<BankStatementImportPreviewDto> {
    const formData = new FormData();
    formData.append("bankId", bankId);
    formData.append("accountId", accountId);
    formData.append("file", file);

    const response = await sendJsonRequest<ImportPreviewResponse>("?handler=PreviewImport", "POST", buildFormHeaders(forgeryToken), formData);
    return unwrapServiceResult<BankStatementImportPreviewDto>(response);
}

export async function importMoneyMovements(forgeryToken: string | null, request: SaveBankStatementImportRequest): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>("?handler=Import", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<boolean>(response);
}
