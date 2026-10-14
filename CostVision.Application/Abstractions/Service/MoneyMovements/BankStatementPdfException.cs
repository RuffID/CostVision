namespace CostVision.Application.Abstractions.Service.MoneyMovements;

/// <summary>Ожидаемый отказ PDF-границы; сообщение безопасно для пользователя.</summary>
public sealed class BankStatementPdfException(string message) : Exception(message);
