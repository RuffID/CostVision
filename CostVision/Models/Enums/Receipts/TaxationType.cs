namespace CostVision.Models.Enums.Document
{
    public enum TaxationType
    {
        Unknown = 0,
        Osn = 1,             // ОСН
        Usn = 2,             // УСН доход
        UsnIncomeOutcome = 4,// УСН доход-расход
        EnvD = 8,            // ЕНВД
        Eshn = 16,           // ЕСХН
        Psn = 32             // ПСН
    }
}