import { ServiceResult, ServiceResultWithData } from "../../../shared/http.js";

export interface AccountDto {
    id: string;
    name: string;
    description: string | null;
    colorHex: string;
    isActive: boolean;
    canManage: boolean;
    ownerName: string;
    accessRole: AccountAccessRole;
}

export interface ShareCandidateDto {
    id: string;
    name: string;
    login: string;
    isSelected: boolean;
    role: AccountAccessRole | null;
}

export interface AccountPayload {
    name: string;
    description: string;
    colorHex: string;
}

export interface AccountUpdatePayload extends AccountPayload {
    accountId: string;
    isActive: boolean;
}

export interface ShareMemberPayload {
    userId: string;
    role: ShareAssignableRole;
}

export interface ShareMembersPayload {
    accountId: string;
    members: ShareMemberPayload[];
}

export interface AccountViewModel {
    id: string;
    name: string;
    description: string;
    colorHex: string;
    isActive: boolean;
    canManage: boolean;
    ownerName: string;
    accessRole: AccountAccessRole;
}

export type AccountsResponse = ServiceResultWithData<AccountDto[]>;
export type AccountResponse = ServiceResultWithData<AccountDto>;
export type ShareCandidatesResponse = ServiceResultWithData<ShareCandidateDto[]>;
export type UpdateMembersResponse = ServiceResult;

export const enum AccountAccessRole {
    Viewer = 0,
    Editor = 1,
    Owner = 2
}

export type ShareAssignableRole = AccountAccessRole.Viewer | AccountAccessRole.Editor;

export const DEFAULT_ACCOUNT_COLOR_HEX = "#0D6EFD";
export const ACCOUNT_ACCESS_ROLE_EDITOR = AccountAccessRole.Editor;
export const ACCOUNT_ACCESS_ROLE_VIEWER = AccountAccessRole.Viewer;
