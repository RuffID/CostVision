using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class UserActivityModelTests
{
    [Fact]
    public async Task OnPostPingAsync_CallsMarkUserActivityUseCaseForCurrentUser()
    {
        User currentUser = TestUsers.Create();

        Mock<IMarkUserActivityUseCase> markUserActivityUseCase = new(MockBehavior.Strict);
        markUserActivityUseCase
            .Setup(useCase => useCase.ExecuteAsync(currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        UserActivityModel model = new(markUserActivityUseCase.Object)
        {
            CurrentUser = currentUser
        };

        JsonResult json = await model.OnPostPingAsync(CancellationToken.None);

        JsonResultAssert.Success(json);
        markUserActivityUseCase.VerifyAll();
    }

    [Fact]
    public async Task OnPostPingAsync_Throws_WhenCurrentUserIsMissing()
    {
        UserActivityModel model = new(Mock.Of<IMarkUserActivityUseCase>(MockBehavior.Strict));

        await Assert.ThrowsAsync<NullReferenceException>(() => model.OnPostPingAsync(CancellationToken.None));
    }
}
