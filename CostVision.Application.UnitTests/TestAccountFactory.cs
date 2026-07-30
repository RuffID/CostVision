using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UnitTests;

internal static class TestAccountFactory
{
    public static Account Create(
        Guid? accountId = null,
        Guid? ownerUserId = null,
        string name = "Account",
        string? description = null,
        string colorHex = "#123456",
        User? owner = null)
    {
        Guid effectiveOwnerUserId = owner?.Id ?? ownerUserId ?? Guid.NewGuid();
        bool isCreated = owner == null
            ? Account.TryCreate(
                name,
                description,
                colorHex,
                effectiveOwnerUserId,
                new DateTime(2026, 1, 1),
                out Account? account,
                out string? error)
            : Account.TryCreate(
                name,
                description,
                colorHex,
                owner,
                new DateTime(2026, 1, 1),
                out account,
                out error);

        if (!isCreated || account == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовый счёт.");

        account.Id = accountId ?? Guid.NewGuid();

        return account;
    }

    public static AccountMember CreateMember(Guid accountId, Guid userId, AccountAccessRole role)
    {
        if (role == AccountAccessRole.Owner)
            return Create(accountId, userId).Members.Single(member => member.Role == AccountAccessRole.Owner);

        Guid ownerUserId = Guid.NewGuid();
        while (ownerUserId == userId)
            ownerUserId = Guid.NewGuid();

        Account account = Create(accountId, ownerUserId);
        if (!account.TryAddMember(userId, role, out AccountMember? member, out string? error) || member == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестового участника счёта.");

        return member;
    }
}
