namespace CostVision.Application.Models.Responses.Results
{
    public class ServiceError
    {
        public ServiceErrorType Type { get; }

        public string Message { get; }

        public ServiceError(ServiceErrorType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Сообщение ошибки обязательно.", nameof(message));

            Type = type;
            Message = message;
        }
    }
}
