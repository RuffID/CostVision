import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult } from "../../../shared/http.js";
import { AccountDto, AccountPayload, AccountResponse, AccountsResponse, AccountUpdatePayload, ShareCandidateDto, ShareCandidatesResponse, ShareMembersPayload, UpdateMembersResponse } from "./models.js";

export async function loadAccountsRequestAsync(includeInactive: boolean): Promise<AccountDto[]> {
    const url = "?handler=Accounts&includeInactive=" + (includeInactive ? "true" : "false");
    const response = await sendJsonRequest<AccountsResponse>(url, "GET", { "Accept": "application/json" });
    return unwrapServiceResult(response);
}

export async function createAccountAsync(antiForgeryToken: string | null, payload: AccountPayload): Promise<AccountDto> {
    const response = await sendJsonRequest<AccountResponse>("?handler=CreateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}

export async function updateAccountAsync(antiForgeryToken: string | null, payload: AccountUpdatePayload): Promise<AccountDto> {
    const response = await sendJsonRequest<AccountResponse>("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}

export async function loadShareCandidatesRequestAsync(antiForgeryToken: string | null, accountId: string): Promise<ShareCandidateDto[]> {
    const response = await sendJsonRequest<ShareCandidatesResponse>("?handler=ShareCandidates&accountId=" + encodeURIComponent(accountId), "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

export async function updateMembersAsync(antiForgeryToken: string | null, payload: ShareMembersPayload): Promise<void> {
    const response = await sendJsonRequest<UpdateMembersResponse>("?handler=UpdateMembers", "POST", buildJsonHeaders(antiForgeryToken), payload);
    unwrapServiceResult(response);
}
