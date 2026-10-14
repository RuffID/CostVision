using System.Linq.Expressions;

namespace CostVision.Application.Extensions;

internal static class ReadQueryOrdering
{
    public static IOrderedQueryable<T> ByDistance<T>(IQueryable<T> source, Expression<Func<T, DateTime>> date,
        DateTime anchor, Expression<Func<T, decimal>> amount, decimal anchorAmount, Expression<Func<T, Guid>> id)
    {
        // Точный порядковый номер 100-нс интервала; DateTime.Ticks и вычитание дат не переводятся SQL Server provider.
        Expression<Func<DateTime, long>> ticks = value =>
            (((long)value.Year - 1) * 365 + ((long)value.Year - 1) / 4 - ((long)value.Year - 1) / 100 +
             ((long)value.Year - 1) / 400 + value.DayOfYear - 1) * TimeSpan.TicksPerDay +
            (long)value.Hour * TimeSpan.TicksPerHour + (long)value.Minute * TimeSpan.TicksPerMinute +
            (long)value.Second * TimeSpan.TicksPerSecond + (long)value.Millisecond * TimeSpan.TicksPerMillisecond +
            (long)value.Microsecond * 10 + value.Nanosecond / 100;
        Expression tickBody = new ReplaceParameter(ticks.Parameters[0], date.Body).Visit(ticks.Body);
        Expression distance = Expression.Call(typeof(Math), nameof(Math.Abs), Type.EmptyTypes,
            Expression.Subtract(tickBody, Expression.Constant(anchor.Ticks)));
        Expression<Func<T, long>> dateDistance = Expression.Lambda<Func<T, long>>(distance, date.Parameters);
        Expression amountDistance = Expression.Call(typeof(Math), nameof(Math.Abs), Type.EmptyTypes,
            Expression.Subtract(amount.Body, Expression.Constant(anchorAmount)));
        Expression<Func<T, decimal>> difference = Expression.Lambda<Func<T, decimal>>(amountDistance, amount.Parameters);

        return source.OrderBy(dateDistance).ThenBy(difference).ThenBy(id);
    }

    private class ReplaceParameter(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                return replacement;
            }

            return node;
        }
    }
}
