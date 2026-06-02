export function getStatusClassName(statusType: string): string {
    if (statusType === "success") {
        return "alert-success";
    }

    if (statusType === "warning") {
        return "alert-warning";
    }

    return "alert-danger";
}
