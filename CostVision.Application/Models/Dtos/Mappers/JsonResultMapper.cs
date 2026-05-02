﻿﻿using CostVision.Application.Models.Responses.Results;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Application.Models.Dtos.Mappers
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

            return new JsonResult(new
            {
                success = true,
                data = result.Data
            })
            {
                StatusCode = 200
            };
        }
    }
}
