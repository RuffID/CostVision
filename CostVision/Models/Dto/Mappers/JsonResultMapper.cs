using CostVision.Models.Responses.Results;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Models.Dto.Mappers
{
    public static class JsonResultMapper
    {
        public static JsonResult ToJsonResult(ServiceResult result)
        {
            if (!result.Success)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = result.Error!.Message
                })
                {
                    StatusCode = result.Error!.StatusCode
                };
            }

            return new JsonResult(new { success = true })
            {
                StatusCode = 200
            };
        }

        public static JsonResult ToJsonResult<T>(ServiceResult<T> result)
        {
            if (!result.Success)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = result.Error!.Message
                })
                {
                    StatusCode = result.Error!.StatusCode
                };
            }

            return new JsonResult(result.Data)
            {
                StatusCode = 200
            };
        }
    }
}
