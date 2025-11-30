namespace CostVision.Models.Enums.Authorization
{
    public enum AccountAccessRole
    {
        Owner = 1,   // полный доступ, может делиться счётом
        Editor = 2,  // может добавлять/редактировать чеки
        Viewer = 3   // только смотреть
    }
}