﻿namespace CostVision.Application.Models.Responses.Results
{
    public class ServiceResult
    {
        public bool Success { get; }
        public ServiceError? Error { get; }

        protected ServiceResult(bool success, ServiceError? error)
        {
            Success = success;
            Error = error;
        }

        public static ServiceResult Ok()
        {
            return new ServiceResult(true, null);
        }

        public static ServiceResult Fail(int statusCode, string message)
        {
            return new ServiceResult(false, new ServiceError(statusCode, message));
        }
    }
}
