using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Extensions;

/// <summary>
/// Согласованные переводимые предикаты документа, авторской копии и присутствия в счёте.
/// </summary>
public static class ReceiptIdentityPredicates
{
    public static Expression<Func<Receipt, bool>> ForDocument(this ReceiptIdentity identity)
    {
        return receipt => receipt.FiscalDriveNumber == identity.FiscalDriveNumber &&
            receipt.FiscalDocumentNumber == identity.FiscalDocumentNumber &&
            receipt.FiscalSign == identity.FiscalSign &&
            receipt.DateTime == identity.DateTime &&
            receipt.TotalSum == identity.TotalSum &&
            receipt.OperationType == identity.OperationType;
    }

    public static Expression<Func<Receipt, bool>> ForAuthorCopy(this ReceiptIdentity identity, Guid authorId)
    {
        return Combine(identity.ForDocument(), receipt => receipt.CreatedByUserId == authorId);
    }

    public static Expression<Func<Receipt, bool>> InAccount(this ReceiptIdentity identity, Guid accountId, Guid excludedReceiptId = default)
    {
        return Combine(identity.ForDocument(), receipt => receipt.Id != excludedReceiptId &&
            receipt.Accounts.Any(link => link.AccountId == accountId));
    }

    private static Expression<Func<Receipt, bool>> Combine(
        Expression<Func<Receipt, bool>> identity,
        Expression<Func<Receipt, bool>> scope)
    {
        Expression scopedBody = new ParameterReplacement(scope.Parameters[0], identity.Parameters[0]).Visit(scope.Body);
        return Expression.Lambda<Func<Receipt, bool>>(
            Expression.AndAlso(identity.Body, scopedBody), identity.Parameters);
    }

    private class ParameterReplacement(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == source)
            {
                return target;
            }

            return base.VisitParameter(node);
        }
    }
}
