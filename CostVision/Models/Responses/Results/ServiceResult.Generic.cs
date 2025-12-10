namespace CostVision.Models.Responses.Results
{
    public class ServiceResult<T>
    {
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
            return new ServiceResult<T>(true, data, null);
        }

        public static ServiceResult<T> Fail(int statusCode, string message)
        {
            return new ServiceResult<T>(false, default, new ServiceError(statusCode, message));
        }
    }
}
