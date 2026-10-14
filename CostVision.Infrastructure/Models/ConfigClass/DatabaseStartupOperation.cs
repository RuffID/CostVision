namespace CostVision.Infrastructure.Models.ConfigClass;

/// <summary>Явный выбор обновления существующей БД либо первой установки.</summary>
public enum DatabaseStartupOperation
{
    UpdateExisting,
    InitializeNew
}
