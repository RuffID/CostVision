namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Ожидаемый конфликт записи чека; не заменяет произвольные ошибки хранения.
/// </summary>
public class ReceiptWriteConflictException(string message, Exception innerException) : Exception(message, innerException);
