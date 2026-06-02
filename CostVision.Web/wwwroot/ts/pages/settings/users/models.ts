import { ServiceResult, ServiceResultWithData } from "../../../shared/http.js";

export interface UserDto {
    id: string;
    name: string;
    login: string;
    isActive: boolean;
    createdAtUtc: string;
    lastLoginAtUtc: string | null;
}

export interface RoleDto {
    id: string;
    name: string;
    roleType: number;
}

export interface UserEditDto {
    id: string;
    name: string;
    login: string;
    roleIds: string[];
}

export interface UserUpsertRequest {
    id: string | null;
    name: string;
    login: string;
    password: string;
    roleIds: string[];
}

export type UserListResponse = ServiceResultWithData<UserDto[]>;
export type RoleListResponse = ServiceResultWithData<RoleDto[]>;
export type UserEditResponse = ServiceResultWithData<UserEditDto>;
export type UserSaveResponse = ServiceResult;
export type ToggleUserActiveResponse = ServiceResultWithData<boolean>;

export const INACTIVE_CHECKBOX_ID = "showInactiveUsers";
export const INACTIVE_STORAGE_KEY = "costvision_users_showInactive";
export const SEARCH_STORAGE_KEY = "costvision_users_search";
