using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.Extensions;

public static class RequestDateValidation
{
    public static ServiceResult ValidateCalendarRange(DateTime? dateFrom, DateTime? dateTo)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateTo.Value.Date < dateFrom.Value.Date)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");
        }

        if (dateTo.HasValue && dateTo.Value.Date == DateTime.MaxValue.Date)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Дата окончания периода не допускает вычисления следующего дня.");
        }

        return ServiceResult.Ok();
    }

    public static ServiceResult TryTimeWindow(DateTime center, decimal hours, out DateTime from, out DateTime to)
    {
        from = default;
        to = default;
        if (hours < 0)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Допуск по времени не может быть отрицательным.");
        }

        long availableTicks = Math.Min(center.Ticks, DateTime.MaxValue.Ticks - center.Ticks);
        decimal maximumHours = availableTicks / (decimal)TimeSpan.TicksPerHour;
        if (hours > maximumHours)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Допуск по времени выходит за поддерживаемый диапазон дат.");
        }

        try
        {
            double minutes = (double)(hours * 60m);
            from = center.AddMinutes(-minutes);
            to = center.AddMinutes(minutes);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Допуск по времени выходит за поддерживаемый диапазон дат.");
        }

        return ServiceResult.Ok();
    }
}
