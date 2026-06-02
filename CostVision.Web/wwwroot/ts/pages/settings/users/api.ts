import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess } from "../../../shared/http.js";
import { RoleDto, RoleListResponse, ToggleUserActiveResponse, UserDto, UserEditDto, UserEditResponse, UserListResponse, UserSaveResponse, UserUpsertRequest } from "./models.js";

export async function loadRolesAsync(antiForgeryToken: string | null): Promise<RoleDto[]> {
    const response = await sendJsonRequest<RoleListResponse>("?handler=RoleList", "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

export async function loadUsersAsync(showInactive: boolean): Promise<UserDto[]> {
    const url = showInactive ? "?handler=UserList&includeInactive=true" : "?handler=UserList";
    const response = await sendJsonRequest<UserListResponse>(url, "GET", { "Accept": "application/json" });
    return unwrapServiceResult(response);
}

export async function getUserAsync(antiForgeryToken: string | null, userId: string): Promise<UserEditDto> {
    const response = await sendJsonRequest<UserEditResponse>(`?handler=User&id=${userId}`, "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

export async function saveUserAsync(antiForgeryToken: string | null, isCreateMode: boolean, dto: UserUpsertRequest): Promise<void> {
    const handler = isCreateMode ? "Create" : "Update";
    const result = await sendJsonRequest<UserSaveResponse>(`?handler=${handler}`, "POST", buildJsonHeaders(antiForgeryToken), dto);
    unwrapServiceSuccess(result);
}

export async function toggleUserActiveAsync(antiForgeryToken: string | null, userId: string): Promise<boolean> {
    const response = await sendJsonRequest<ToggleUserActiveResponse>(`?handler=ToggleActive&id=${userId}`, "POST", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}
