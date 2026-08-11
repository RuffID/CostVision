namespace CostVision.Domain.Models.Enums.Receipts
{
    /// <summary>
    /// Признак предмета расчёта (тег 1212 ФФД).
    /// </summary>
    public enum ProductType
    {
        Product = 1,
        Excise = 2,
        Work = 3,
        Service = 4,
        GamblingBet = 5,
        GamblingPrize = 6,
        Lottery = 7,
        LotteryPrize = 8,
        IntellectualActivity = 9,
        Payment = 10,
        AgentReward = 11,
        Payout = 12,
        Other = 13,
        PropertyRight = 14,
        NonOperatingIncome = 15,
        OtherPaymentsAndContributions = 16,
        TradeFee = 17,
        TouristTax = 18,
        Deposit = 19,
        Expense = 20,
        PensionInsuranceIp = 21,
        PensionInsurance = 22,
        MedicalInsuranceIp = 23,
        MedicalInsurance = 24,
        SocialInsurance = 25,
        CasinoPayment = 26,
        CashWithdrawal = 27,
        MarkedExciseWithoutCode = 30,
        MarkedExciseWithCode = 31,
        MarkedProductWithoutCode = 32,
        MarkedProductWithCode = 33
    }
}
