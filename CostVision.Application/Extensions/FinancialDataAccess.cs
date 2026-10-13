using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.Extensions;

/// <summary>
/// Единая область чтения финансовых данных; системная роль не расширяет доступ.
/// </summary>
internal static class FinancialDataAccess
{
    public static AccountAccessRole GetRequiredAccountRole(Account account, Guid currentUserId)
    {
        List<AccountMember> ownerMemberships = account.Members
            .Where(member => member.UserId == account.CreatedByUserId).ToList();
        if (ownerMemberships.Count != 1 || ownerMemberships[0].Role != AccountAccessRole.Owner)
        {
            throw new InvalidOperationException("Нарушен инвариант Owner membership счёта.");
        }

        if (account.Members.Any(member => member.Role == AccountAccessRole.Owner && member.UserId != account.CreatedByUserId))
        {
            throw new InvalidOperationException("Owner membership не соответствует владельцу счёта.");
        }

        List<AccountMember> memberships = account.Members
            .Where(member => member.UserId == currentUserId).ToList();
        if (memberships.Count != 1 || !Enum.IsDefined(memberships[0].Role))
        {
            throw new InvalidOperationException("Нарушен инвариант роли участника доступного счёта.");
        }

        AccountAccessRole role = memberships[0].Role;
        return role;
    }

    public static bool CanReadAccount(Account? account, Guid currentUserId)
    {
        if (account == null)
        {
            return false;
        }

        if (account.CreatedByUserId == currentUserId)
        {
            return true;
        }

        foreach (AccountMember member in account.Members)
        {
            if (member.UserId == currentUserId)
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanReadReceipt(Receipt? receipt, Guid currentUserId)
    {
        if (receipt == null)
        {
            return false;
        }

        if (receipt.CreatedByUserId == currentUserId)
        {
            return true;
        }

        foreach (ReceiptAccount link in receipt.Accounts)
        {
            if (CanReadAccount(link.Account, currentUserId))
            {
                return true;
            }
        }

        return false;
    }

    public static Expression<Func<T, bool>> WithAccountReadAccess<T>(
        this Expression<Func<T, bool>> predicate, Expression<Func<T, Account?>> accountSelector, Guid currentUserId)
    {
        Expression<Func<Account?, bool>> accountExists = account => account != null;
        Expression<Func<Account?, bool>> userOwnsAccount = account => account!.CreatedByUserId == currentUserId;
        Expression<Func<Account?, bool>> userIsMember = account => account!.Members.Any(member => member.UserId == currentUserId);

        Expression<Func<Account?, bool>> userHasAccess = JoinConditions(userOwnsAccount, userIsMember, matchAny: true);
        Expression<Func<Account?, bool>> access = JoinConditions(accountExists, userHasAccess, matchAny: false);

        return Combine(predicate, accountSelector, access);
    }

    public static Expression<Func<T, bool>> WithReceiptReadAccess<T>(
        this Expression<Func<T, bool>> predicate, Expression<Func<T, Receipt?>> receiptSelector, Guid currentUserId)
    {
        Expression<Func<Receipt?, bool>> receiptExists = receipt => receipt != null;
        Expression<Func<Receipt?, bool>> userCreatedReceipt = receipt => receipt!.CreatedByUserId == currentUserId;
        Expression<Func<Receipt?, bool>> userOwnsLinkedAccount = receipt => receipt!.Accounts.Any(link =>
            link.Account != null && link.Account.CreatedByUserId == currentUserId);
        Expression<Func<Receipt?, bool>> userIsLinkedAccountMember = receipt => receipt!.Accounts.Any(link =>
            link.Account != null && link.Account.Members.Any(member => member.UserId == currentUserId));

        Expression<Func<Receipt?, bool>> linkedAccountAccess = JoinConditions(userOwnsLinkedAccount, userIsLinkedAccountMember, matchAny: true);
        Expression<Func<Receipt?, bool>> userHasAccess = JoinConditions(userCreatedReceipt, linkedAccountAccess, matchAny: true);
        Expression<Func<Receipt?, bool>> access = JoinConditions(receiptExists, userHasAccess, matchAny: false);

        return Combine(predicate, receiptSelector, access);
    }

    private static Expression<Func<T, bool>> Combine<T, TRelated>(Expression<Func<T, bool>> predicate,
        Expression<Func<T, TRelated>> selector, Expression<Func<TRelated, bool>> access)
    {
        Expression related = new ParameterReplacement(selector.Parameters[0], predicate.Parameters[0]).Visit(selector.Body);
        Expression accessBody = new ParameterReplacement(access.Parameters[0], related).Visit(access.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(predicate.Body, accessBody), predicate.Parameters);
    }

    private static Expression<Func<T, bool>> JoinConditions<T>(
        Expression<Func<T, bool>> first, Expression<Func<T, bool>> second, bool matchAny)
    {
        Expression secondBody = new ParameterReplacement(second.Parameters[0], first.Parameters[0]).Visit(second.Body);
        BinaryExpression combinedBody;
        if (matchAny)
        {
            combinedBody = Expression.OrElse(first.Body, secondBody);
        }
        else
        {
            combinedBody = Expression.AndAlso(first.Body, secondBody);
        }

        return Expression.Lambda<Func<T, bool>>(combinedBody, first.Parameters);
    }

    // Подстановка сохраняет обычное дерево LINQ без Invoke и вызовов скомпилированных функций в SQL-предикате.
    private class ParameterReplacement(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                return replacement;
            }
            else
            {
                return node;
            }
        }
    }
}
