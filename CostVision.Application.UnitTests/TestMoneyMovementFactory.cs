using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using System.Reflection;

namespace CostVision.Application.UnitTests;

internal static class TestMoneyMovementFactory
{
    public static MoneyMovement CreateManual(
        Guid? movementId = null,
        Guid? accountId = null,
        decimal amount = 100m,
        DateTime? occurredAt = null,
        Guid? userId = null,
        string? accountName = "Account",
        string? performedByUserName = "User",
        MoneyMovementType? type = null,
        string? comment = null,
        Guid? performedByUserId = null,
        string? accountColorHex = null)
    {
        Guid effectiveUserId = userId ?? Guid.NewGuid();
        Guid effectivePerformedByUserId = performedByUserId ?? effectiveUserId;
        Account account = TestAccountFactory.Create(
            accountId,
            effectiveUserId,
            accountName ?? "Account",
            colorHex: accountColorHex ?? "#123456");
        User createdByUser = CreateUser(effectiveUserId, "Creator");
        User performedByUser = performedByUserName == null
            ? createdByUser
            : CreateUser(effectivePerformedByUserId, performedByUserName);
        MoneyMovementType effectiveType = type ?? (amount < 0 ? MoneyMovementType.Expense : MoneyMovementType.Income);

        bool isCreated = MoneyMovement.TryCreateManual(
            account,
            amount,
            effectiveType,
            occurredAt ?? new DateTime(2026, 1, 1),
            comment,
            createdByUser,
            performedByUser,
            new DateTime(2026, 1, 1),
            out MoneyMovement? movement,
            out string? error);
        if (!isCreated || movement == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую операцию.");

        movement.Id = movementId ?? Guid.NewGuid();
        return movement;
    }

    public static MoneyMovement CreateManualWithoutNavigations(
        Guid? movementId = null,
        Guid? accountId = null,
        decimal amount = 100m,
        DateTime? occurredAt = null,
        Guid? userId = null,
        MoneyMovementType? type = null,
        string? comment = null)
    {
        Guid effectiveUserId = userId ?? Guid.NewGuid();
        bool isCreated = MoneyMovement.TryCreateManual(
            accountId ?? Guid.NewGuid(),
            amount,
            type ?? (amount < 0 ? MoneyMovementType.Expense : MoneyMovementType.Income),
            occurredAt ?? new DateTime(2026, 1, 1),
            comment,
            effectiveUserId,
            effectiveUserId,
            new DateTime(2026, 1, 1),
            out MoneyMovement? movement,
            out string? error);
        if (!isCreated || movement == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую операцию.");

        movement.Id = movementId ?? Guid.NewGuid();
        return movement;
    }

    public static MoneyMovement CreateBankStatementImport(
        Guid? movementId = null,
        Guid? accountId = null,
        decimal amount = 100m,
        DateTime? occurredAt = null,
        Guid? userId = null,
        MoneyMovementType type = MoneyMovementType.Expense,
        string? comment = null,
        string importComment = "Imported operation",
        string accountName = "Account",
        string accountColorHex = "#123456",
        string userName = "User")
    {
        Guid effectiveUserId = userId ?? Guid.NewGuid();
        Account account = TestAccountFactory.Create(
            accountId,
            effectiveUserId,
            accountName,
            colorHex: accountColorHex);
        User user = CreateUser(effectiveUserId, userName);
        bool isCreated = MoneyMovement.TryCreateBankStatementImport(
            account,
            amount,
            type,
            occurredAt ?? new DateTime(2026, 1, 1),
            comment,
            importComment,
            user,
            new DateTime(2026, 1, 1),
            out MoneyMovement? movement,
            out string? error);
        if (!isCreated || movement == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую импортированную операцию.");

        movement.Id = movementId ?? Guid.NewGuid();
        return movement;
    }

    public static MoneyMovementReceipt CreateLink(
        Guid moneyMovementId,
        Guid receiptId,
        Guid? createdByUserId = null)
    {
        bool isCreated = MoneyMovementReceipt.TryCreate(
            moneyMovementId,
            receiptId,
            createdByUserId ?? Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            out MoneyMovementReceipt? link,
            out string? error);
        if (!isCreated || link == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую связь операции с чеком.");

        return link;
    }

    public static MoneyMovementReceipt CreateLink(
        MoneyMovement movement,
        Receipt receipt,
        Guid? createdByUserId = null)
    {
        bool isCreated = MoneyMovementReceipt.TryCreate(
            movement,
            receipt,
            createdByUserId ?? Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            out MoneyMovementReceipt? link,
            out string? error);
        if (!isCreated || link == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую связь операции с чеком.");

        return link;
    }

    public static void AddMaterializedReceiptLink(
        MoneyMovement movement,
        MoneyMovementReceipt link)
    {
        FieldInfo field = typeof(MoneyMovement).GetField(
            "_receiptLinks",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Backing field связей операции не найден.");
        List<MoneyMovementReceipt> links = (List<MoneyMovementReceipt>)field.GetValue(movement)!;
        links.Add(link);
    }

    private static User CreateUser(Guid userId, string name)
    {
        bool isCreated = User.TryCreate(
            $"user-{Guid.NewGuid():N}",
            name,
            "hash",
            [Guid.NewGuid()],
            new DateTime(2026, 1, 1),
            out User? user,
            out string? error);
        if (!isCreated || user == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестового пользователя.");

        user.Id = userId;
        return user;
    }
}
