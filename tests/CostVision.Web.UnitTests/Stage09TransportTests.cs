using System.Reflection;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests;

public class Stage09TransportTests
{
    public static TheoryData<Type> Pages = new()
    {
        typeof(AddReceiptModel), typeof(ReceiptsModel), typeof(MoneyMovementsModel),
        typeof(IndexModel), typeof(ProductsModel), typeof(StoresModel),
        typeof(CostVision.Web.Pages.Settings.UsersModel), typeof(CostVision.Web.Pages.Settings.UserSettingsModel)
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task InvalidBinding_RejectsEveryHandlerBeforeAnyUseCase(Type pageType)
    {
        // Arrange
        PageModel page = CreatePage(pageType);
        page.ModelState.AddModelError("request", "Invalid Guid, date, number or malformed JSON.");
        MethodInfo[] handlers = pageType.GetMethods().Where(method => method.Name.StartsWith("On") &&
            method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)).ToArray();

        foreach (MethodInfo handler in handlers)
        {
            object?[] arguments = handler.GetParameters().Select(CreateArgument).ToArray();

            // Act
            JsonResult result = await InvokeAsync(page, handler, arguments);

            // Assert
            Assert.Equal(400, result.StatusCode);
            JsonResultAssert.Failure(result, 400, "Некорректные данные запроса. Проверьте даты, числа и обязательные поля.");
        }

        Assert.NotEmpty(handlers);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task NullDto_RejectsEveryBoundRequestBeforeAnyUseCase(Type pageType)
    {
        // Arrange
        PageModel page = CreatePage(pageType);
        MethodInfo[] handlers = pageType.GetMethods().Where(method => method.GetParameters().Any(parameter =>
            parameter.Name is "request" or "input" or "dto")).ToArray();

        foreach (MethodInfo handler in handlers)
        {
            object?[] arguments = handler.GetParameters().Select(parameter =>
            {
                if (parameter.Name is "request" or "input" or "dto")
                {
                    return null;
                }

                return CreateArgument(parameter);
            }).ToArray();

            // Act
            JsonResult result = await InvokeAsync(page, handler, arguments);

            // Assert
            JsonResultAssert.Failure(result, 400, "Отсутствуют данные запроса.");
        }

        Assert.NotEmpty(handlers);
    }

    public static TheoryData<Type, string, object> InvalidShapes = new()
    {
        { typeof(AddReceiptModel), "OnPostAsync", new QrScanRequest { Results = null! } },
        { typeof(AddReceiptModel), "OnPostAsync", new QrScanRequest { Results = [null!] } },
        { typeof(AddReceiptModel), "OnPostManualAsync", new ReceiptManualCreateRequest { Receipt = null! } },
        { typeof(MoneyMovementsModel), "OnPostImportAsync", new SaveBankStatementImportRequest { Rows = null! } },
        { typeof(MoneyMovementsModel), "OnPostImportAsync", new SaveBankStatementImportRequest { Rows = [null!] } },
        { typeof(CostVision.Web.Pages.Settings.UserSettingsModel), "OnPostUpdateMembersAsync", new UpdateAccountMembersRequest { Members = null! } },
        { typeof(CostVision.Web.Pages.Settings.UserSettingsModel), "OnPostUpdateMembersAsync", new UpdateAccountMembersRequest { Members = [null!] } },
        { typeof(CostVision.Web.Pages.Settings.UsersModel), "OnPostCreateAsync", new UserUpsertRequest { RoleIds = null! } },
        { typeof(IndexModel), "OnGetIncomeExpenseReportAsync", new DashboardIncomeExpenseReportRequest { AccountIds = null! } },
        { typeof(IndexModel), "OnGetIncomeExpenseReportAsync", new DashboardIncomeExpenseReportRequest { StoreNames = null! } },
        { typeof(IndexModel), "OnGetIncomeExpenseReportAsync", new DashboardIncomeExpenseReportRequest { StoreNames = [null!] } }
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public async Task InvalidCollectionsAndNestedDto_DoNotReachUseCase(Type pageType, string handlerName, object request)
    {
        // Arrange
        PageModel page = CreatePage(pageType);
        MethodInfo handler = pageType.GetMethod(handlerName)!;

        // Act
        JsonResult result = await InvokeAsync(page, handler, [request, CancellationToken.None]);

        // Assert
        JsonResultAssert.Failure(result, 400, "Некорректная форма запроса: проверьте обязательные данные и элементы списка.");
    }

    private static PageModel CreatePage(Type pageType)
    {
        ConstructorInfo constructor = pageType.GetConstructors().Single();
        List<object> arguments = [];
        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            Type mockType = typeof(Mock<>).MakeGenericType(parameter.ParameterType);
            Mock mock = (Mock)Activator.CreateInstance(mockType, MockBehavior.Strict)!;
            arguments.Add(mock.Object);
        }

        PageModel page = (PageModel)constructor.Invoke(arguments.ToArray());
        ((IHasCurrentUser)page).CurrentUser = TestUsers.Create();
        return page;
    }

    private static object? CreateArgument(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }
        if (parameter.ParameterType == typeof(string))
        {
            return "test";
        }
        if (parameter.ParameterType.IsInterface)
        {
            Type mockType = typeof(Mock<>).MakeGenericType(parameter.ParameterType);
            return ((Mock)Activator.CreateInstance(mockType, MockBehavior.Strict)!).Object;
        }

        return Activator.CreateInstance(parameter.ParameterType);
    }

    private static async Task<JsonResult> InvokeAsync(PageModel page, MethodInfo handler, object?[] arguments)
    {
        Task task = (Task)handler.Invoke(page, arguments)!;
        await task;
        object? result = task.GetType().GetProperty("Result")!.GetValue(task);
        return Assert.IsType<JsonResult>(result);
    }
}
