using System.Diagnostics.CodeAnalysis;

namespace CostVision.Application.Models.Responses.Results
{
    public class ServiceResult<T>
    {
        [MemberNotNullWhen(true, nameof(Data))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool Success { get; }

        public ServiceError? Error { get; }

        public T? Data { get; }

        private ServiceResult(bool success, T? data, ServiceError? error)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        public static ServiceResult<T> Ok(T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return new ServiceResult<T>(true, data, null);
        }

        public static ServiceResult<T> Fail(ServiceError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ServiceResult<T>(false, default, error);
        }

        public static ServiceResult<T> Fail(ServiceErrorType type, string message)
        {
            return Fail(new ServiceError(type, message));
        }

        public ServiceResult PropagateFailure()
        {
            if (Success)
                throw new InvalidOperationException("Успешный результат нельзя передать как ошибку.");

            return ServiceResult.Fail(Error);
        }

        public ServiceResult<TTarget> PropagateFailure<TTarget>()
        {
            if (Success)
                throw new InvalidOperationException("Успешный результат нельзя передать как ошибку.");

            return ServiceResult<TTarget>.Fail(Error);
        }
    }
}
