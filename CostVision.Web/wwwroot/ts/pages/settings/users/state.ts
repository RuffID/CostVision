import { UserDto } from "./models.js";

export function filterUsers(users: UserDto[], showInactive: boolean, searchText: string): UserDto[] {
    let result = users.slice();

    if (searchText) {
        result = result.filter(function (user: UserDto) {
            return user.name.toLowerCase().includes(searchText) || user.login.toLowerCase().includes(searchText);
        });
    }

    if (!showInactive) {
        result = result.filter(function (user: UserDto) {
            return user.isActive === true;
        });
    }

    return result;
}
