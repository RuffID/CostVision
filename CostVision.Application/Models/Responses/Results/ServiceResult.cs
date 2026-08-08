using System.Diagnostics.CodeAnalysis;

namespace CostVision.Application.Models.Responses.Results
{
    public class ServiceResult
    {
        [MemberNotNullWhen(false, nameof(Error))]
        public bool Success { get; }

        public ServiceError? Error { get; }

        private ServiceResult(bool success, ServiceError? error)
        {
            Success = success;
            Error = error;
        }

        public static ServiceResult Ok()
        {
            return new ServiceResult(true, null);
        }

        public static ServiceResult Fail(ServiceError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ServiceResult(false, error);
        }

        public static ServiceResult Fail(ServiceErrorType type, string message)
        {
            return Fail(new ServiceError(type, message));
        }

        public ServiceResult<TTarget> PropagateFailure<TTarget>()
        {
            if (Success)
                throw new InvalidOperationException("Успешный результат нельзя передать как ошибку.");

            return ServiceResult<TTarget>.Fail(Error);
        }

        public ServiceResult PropagateFailure()
        {
            if (Success)
                throw new InvalidOperationException("Успешный результат нельзя передать как ошибку.");

            return Fail(Error);
        }
    }
}
