using CostVision.Application.Models.Responses.Results;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Web.Mappers
{
    public static class JsonResultMapper
    {
        public static JsonResult ToJsonResult(ServiceResult result)
        {
            if (!result.Success)
                return CreateFailure(result.Error);

            return new JsonResult(new { success = true })
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        public static JsonResult ToJsonResult<T>(ServiceResult<T> result)
        {
            if (!result.Success)
                return CreateFailure(result.Error);

            return new JsonResult(new
            {
                success = true,
                data = result.Data
            })
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        private static JsonResult CreateFailure(ServiceError error)
        {
            return new JsonResult(new
            {
                success = false,
                message = error.Message
            })
            {
                StatusCode = MapStatusCode(error.Type)
            };
        }

        internal static int MapStatusCode(ServiceErrorType errorType)
        {
            return errorType switch
            {
                ServiceErrorType.Validation => StatusCodes.Status400BadRequest,
                ServiceErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
                ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
                ServiceErrorType.ExternalService => StatusCodes.Status502BadGateway,
                _ => throw new ArgumentOutOfRangeException(nameof(errorType), errorType, "Неизвестный тип сервисной ошибки.")
            };
        }
    }
}
