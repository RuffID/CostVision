export interface DashboardIncomeExpensePointDto {
    periodStart: string;
    label: string;
    incomeSum: number;
    expenseSum: number;
}

export interface DashboardIncomeExpenseReportDto {
    stores: string[];
    points: DashboardIncomeExpensePointDto[];
}

export interface DashboardAccountDto {
    id: string;
    name: string;
    colorHex: string;
}
