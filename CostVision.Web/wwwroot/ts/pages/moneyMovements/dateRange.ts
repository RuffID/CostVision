export interface MoneyMovementDateRange {
    dateFrom: Date;
    dateTo: Date;
}

export function getDateRangeByPeriodPreset(periodPreset: string, now: Date): MoneyMovementDateRange {
    const currentDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    if (periodPreset === "currentDay") {
        return {
            dateFrom: currentDate,
            dateTo: currentDate
        };
    }

    if (periodPreset === "previousDay") {
        const previousDay = new Date(currentDate);
        previousDay.setDate(currentDate.getDate() - 1);

        return {
            dateFrom: previousDay,
            dateTo: previousDay
        };
    }

    if (periodPreset === "currentWeek") {
        const dayOfWeek = currentDate.getDay();
        const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const dateFrom = new Date(currentDate);
        dateFrom.setDate(currentDate.getDate() - daysFromMonday);

        const dateTo = new Date(dateFrom);
        dateTo.setDate(dateFrom.getDate() + 6);

        return {
            dateFrom: dateFrom,
            dateTo: dateTo
        };
    }

    if (periodPreset === "previousWeek") {
        const dayOfWeek = currentDate.getDay();
        const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const dateTo = new Date(currentDate);
        dateTo.setDate(currentDate.getDate() - daysFromMonday - 1);

        const dateFrom = new Date(dateTo);
        dateFrom.setDate(dateTo.getDate() - 6);

        return {
            dateFrom: dateFrom,
            dateTo: dateTo
        };
    }

    if (periodPreset === "currentYear") {
        return {
            dateFrom: new Date(currentDate.getFullYear(), 0, 1),
            dateTo: new Date(currentDate.getFullYear(), 11, 31)
        };
    }

    if (periodPreset === "previousMonth") {
        return {
            dateFrom: new Date(currentDate.getFullYear(), currentDate.getMonth() - 1, 1),
            dateTo: new Date(currentDate.getFullYear(), currentDate.getMonth(), 0)
        };
    }

    return {
        dateFrom: new Date(currentDate.getFullYear(), currentDate.getMonth(), 1),
        dateTo: new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0)
    };
}

export function formatDateForQuery(date: Date): string {
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, "0");
    const day = date.getDate().toString().padStart(2, "0");
    return year + "-" + month + "-" + day;
}
