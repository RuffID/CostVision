namespace CostVision.Models.Responses.Results
{
    public class ServiceError(int statusCode, string message)
    {
        public int StatusCode { get; } = statusCode;
        public string Message { get; } = message;
    }
}
