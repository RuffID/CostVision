export interface EditableAccount {
    accessRole: number | null;
    canManage: boolean;
}

export function canEditAccount(account: EditableAccount): boolean {
    return account.canManage === true || account.accessRole === 1 || account.accessRole === 2;
}
