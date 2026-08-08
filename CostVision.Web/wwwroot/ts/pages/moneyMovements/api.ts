import { buildFormHeaders, buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import type { BankStatementImportBankDto, BankStatementImportPreviewDto, BankStatementImportResultDto, CreateMoneyMovementRequest, DeleteMoneyMovementRequest, GetMoneyMovementReceiptCandidatesRequest, LinkMoneyMovementReceiptRequest, MoneyMovementDto, MoneyMovementReceiptDto, MoveMoneyMovementToAccountRequest, SaveBankStatementImportRequest, UnlinkMoneyMovementReceiptRequest, UpdateMoneyMovementCommentRequest, UserAccountViewModel, ReceiptDto } from "./types.js";

type AccountListResponse = ServiceResultWithData<UserAccountViewModel[]>;
type ImportBankListResponse = ServiceResultWithData<BankStatementImportBankDto[]>;
type ImportPreviewResponse = ServiceResultWithData<BankStatementImportPreviewDto>;
type MoneyMovementListResponse = ServiceResultWithData<MoneyMovementDto[]>;
type MoneyMovementResponse = ServiceResultWithData<MoneyMovementDto>;
type MoneyMovementReceiptListResponse = ServiceResultWithData<MoneyMovementReceiptDto[]>;
type ReceiptResponse = ServiceResultWithData<ReceiptDto>;
type ImportResultResponse = ServiceResultWithData<BankStatementImportResultDto>;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

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

export async function moveMoneyMovementToAccount(forgeryToken: string | null, request: MoveMoneyMovementToAccountRequest): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=MoveToAccount", "POST", buildJsonHeaders(forgeryToken), {
        ...request,
        sourceAccountId: request.sourceAccountId || EMPTY_GUID
    });
    unwrapServiceSuccess(response);
}

export async function deleteMoneyMovement(forgeryToken: string | null, request: DeleteMoneyMovementRequest): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=Delete", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}

export async function updateMoneyMovementComment(forgeryToken: string | null, request: UpdateMoneyMovementCommentRequest): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=UpdateComment", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}

export async function loadLinkedReceipts(forgeryToken: string | null, moneyMovementId: string): Promise<MoneyMovementReceiptDto[]> {
    const params = new URLSearchParams();
    params.set("moneyMovementId", moneyMovementId);

    const response = await sendJsonRequest<MoneyMovementReceiptListResponse>(`?handler=LinkedReceipts&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<MoneyMovementReceiptDto[]>(response);
}

export async function loadReceiptCandidates(forgeryToken: string | null, request: GetMoneyMovementReceiptCandidatesRequest): Promise<MoneyMovementReceiptDto[]> {
    const params = new URLSearchParams();
    params.set("moneyMovementId", request.moneyMovementId);
    params.set("useTimeWindow", String(request.useTimeWindow));
    params.set("useAmountFilter", String(request.useAmountFilter));
    params.set("excludeLinkedReceipts", String(request.excludeLinkedReceipts));

    if (request.dateFrom) {
        params.set("dateFrom", request.dateFrom);
    }

    if (request.dateTo) {
        params.set("dateTo", request.dateTo);
    }

    if (request.amountTolerance !== null) {
        params.set("amountTolerance", String(request.amountTolerance));
    }

    if (request.timeWindowHours !== null) {
        params.set("timeWindowHours", String(request.timeWindowHours));
    }

    const response = await sendJsonRequest<MoneyMovementReceiptListResponse>(`?handler=ReceiptCandidates&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<MoneyMovementReceiptDto[]>(response);
}

export async function openReceipt(forgeryToken: string | null, receiptId: string): Promise<ReceiptDto> {
    const response = await sendJsonRequest<ReceiptResponse>(
        "?handler=OpenReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    return unwrapServiceResult<ReceiptDto>(response);
}

export async function linkMoneyMovementReceipt(forgeryToken: string | null, request: LinkMoneyMovementReceiptRequest): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=LinkReceipt", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}

export async function unlinkMoneyMovementReceipt(forgeryToken: string | null, request: UnlinkMoneyMovementReceiptRequest): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=UnlinkReceipt", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}

export async function previewBankStatementImport(forgeryToken: string | null, bankId: string, accountId: string, file: File): Promise<BankStatementImportPreviewDto> {
    const formData = new FormData();
    formData.append("bankId", bankId);
    formData.append("accountId", accountId);
    formData.append("file", file);

    const response = await sendJsonRequest<ImportPreviewResponse>("?handler=PreviewImport", "POST", buildFormHeaders(forgeryToken), formData);
    return unwrapServiceResult<BankStatementImportPreviewDto>(response);
}

export async function importMoneyMovements(forgeryToken: string | null, request: SaveBankStatementImportRequest): Promise<BankStatementImportResultDto> {
    const response = await sendJsonRequest<ImportResultResponse>("?handler=Import", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<BankStatementImportResultDto>(response);
}
