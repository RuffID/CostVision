namespace CostVision.Models.Enums.Receipts
{
    public enum ProductType
    {
        // 1 — Товар (кроме подакцизных и маркируемых)
        Goods = 1,

        // 2 — Подакцизный товар (кроме маркируемых)
        ExciseGoods = 2,

        // 3 — Работа
        Work = 3,

        // 4 — Услуга
        Service = 4,

        // 5 — Ставка азартной игры
        GamblingBet = 5,

        // 6 — Выигрыш азартной игры
        GamblingWin = 6,

        // 7 — Лотерейный билет / ставка лотереи
        LotteryTicketOrBet = 7,

        // 8 — Выигрыш лотереи
        LotteryWin = 8,

        // 9 — Предоставление прав на использование РИД
        IntellectualProperty = 9,

        // 10 — Платеж (аванс, задаток, предоплата, кредит)
        Payment = 10,

        // 11 — Агентское вознаграждение
        AgencyFee = 11,

        // 12 — Выплата (взнос, пеня, штраф, бонус и т.д.)
        Payout = 12,

        // 13 — Иной предмет расчета
        Other = 13,

        // 14 — Передача имущественных прав
        PropertyRights = 14,

        // 15 — Внереализационный доход
        NonOperatingIncome = 15,

        // 16 — Иные платежи и взносы (налог уменьшающие)
        OtherPaymentsAndFees = 16,

        // 17 — Торговый сбор
        TradeFee = 17,

        // 18 — Туристический налог
        TouristTax = 18,

        // 19 — Залог
        Deposit = 19,

        // 20 — Расход (уменьшает доход)
        Expense = 20,

        // 21 — Взносы ОПС ИП (без работников)
        PensionContributionsIP = 21,

        // 22 — Взносы ОПС (организации/ИП с работниками)
        PensionContributions = 22,

        // 23 — Взносы ОМС ИП (без работников)
        MedicalContributionsIP = 23,

        // 24 — Взносы ОМС (организации/ИП с работниками)
        MedicalContributions = 24,

        // 25 — Взносы ОСС
        SocialInsuranceContributions = 25,

        // 26 — Платеж казино
        CasinoPayment = 26,

        // 27 — Выдача денежных средств банковским платежным агентом
        CashWithdrawalAgent = 27,

        // 30 — Подакцизный маркируемый товар без кода маркировки
        ExciseMarkedNoCode = 30,

        // 31 — Подакцизный маркируемый товар с кодом маркировки
        ExciseMarkedWithCode = 31,

        // 32 — Маркируемый товар без кода (не подакцизный)
        MarkedNoCode = 32,

        // 33 — Маркируемый товар с кодом (не подакцизный)
        MarkedWithCode = 33
    }
}
