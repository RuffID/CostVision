using CostVision.Application.Extensions;
using CostVision.Application.Models.Responses.Results;
using CostVision.Web.Mappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Extensions;

public static class PageRequestValidationExtensions
{
    public static JsonResult? ValidateBinding(this PageModel page)
    {
        if (!page.ModelState.IsValid)
        {
            return JsonResultMapper.ToJsonResult(ServiceResult.Fail(ServiceErrorType.Validation,
                "Некорректные данные запроса. Проверьте даты, числа и обязательные поля."));
        }

        return null;
    }

    public static JsonResult? ValidateRequest(this PageModel page, object? request)
    {
        JsonResult? bindingError = page.ValidateBinding();
        if (bindingError != null)
        {
            return bindingError;
        }

        ServiceResult shape = RequestShapeValidation.Validate(request);
        if (!shape.Success)
        {
            return JsonResultMapper.ToJsonResult(shape);
        }

        return null;
    }
}
