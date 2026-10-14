using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.Extensions;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class MoneyMovementsModelTests
{
    [Fact]
    public async Task OnGetListAndCandidates_PreserveExplicitPagingFiltersAndFullTotals()
    {
        // Arrange
        User user = TestUsers.Create();
        DateTime date = new(2026, 1, 15);
        Guid accountId = Guid.NewGuid();
        GetMoneyMovementReceiptCandidatesRequest request = new()
        {
            MoneyMovementId = Guid.NewGuid(), Page = 2, PageSize = 1
        };
        MoneyMovementListDto expectedList = new()
        {
            Items = [new MoneyMovementDto { Amount = 1m }], Page = 3, PageSize = 5,
            TotalCount = 30, IncomeSum = 500m, ExpenseSum = 1000m
        };
        PagedListDto<MoneyMovementReceiptDto> expectedCandidates = PagedListDto<MoneyMovementReceiptDto>.Create(
            [new MoneyMovementReceiptDto(), new MoneyMovementReceiptDto()], 4, 2, 1);
        Dependencies dependencies = new();
        dependencies.GetList.Setup(useCase => useCase.ExecuteAsync(user.Id, date, date, accountId,
            It.IsAny<CancellationToken>(), 3, 5, "coffee", "withReceipts"))
            .ReturnsAsync(ServiceResult<MoneyMovementListDto>.Ok(expectedList));
        dependencies.GetReceiptCandidates.Setup(useCase => useCase.ExecuteAsync(request, user.Id,
            It.IsAny<CancellationToken>())).ReturnsAsync(ServiceResult<PagedListDto<MoneyMovementReceiptDto>>.Ok(expectedCandidates));
        MoneyMovementsModel model = dependencies.CreateModel(user);

        // Act
        JsonResult listResult = await model.OnGetListAsync(date, date, accountId, CancellationToken.None, 3, 5, "coffee", "withReceipts");
        JsonResult candidatesResult = await model.OnGetReceiptCandidatesAsync(request, CancellationToken.None);

        // Assert
        MoneyMovementListDto list = JsonResultAssert.Data<MoneyMovementListDto>(listResult);
        PagedListDto<MoneyMovementReceiptDto> candidates = JsonResultAssert.Data<PagedListDto<MoneyMovementReceiptDto>>(candidatesResult);
        Assert.Same(expectedList, list);
        Assert.Equal(30, list.TotalCount);
        Assert.Equal(1000m, list.ExpenseSum);
        Assert.Equal(4, candidates.TotalCount);
        Assert.Equal(2, candidates.Items.Count);
        Assert.Equal(1, candidates.PageSize);
        dependencies.GetList.VerifyAll();
        dependencies.GetReceiptCandidates.VerifyAll();
    }

    [Fact]
    public async Task OnGetListAsync_CallsListUseCase()
    {
        // Arrange
        User currentUser = TestUsers.Create();
        DateTime dateFrom = new(2026, 5, 1);
        DateTime dateTo = new(2026, 5, 2);
        Guid accountId = Guid.NewGuid();

        Dependencies dependencies = new();
        dependencies.GetList
            .Setup(useCase => useCase.ExecuteAsync(currentUser.Id, dateFrom, dateTo, accountId, It.IsAny<CancellationToken>(), 1, 20, null, null))
            .ReturnsAsync(ServiceResult<MoneyMovementListDto>.Ok(new MoneyMovementListDto { Items = [new MoneyMovementDto { Id = Guid.NewGuid() }] }));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        // Act
        JsonResult json = await model.OnGetListAsync(dateFrom, dateTo, accountId, CancellationToken.None);

        // Assert
        Assert.Single(JsonResultAssert.Data<MoneyMovementListDto>(json).Items);
        dependencies.GetList.VerifyAll();
    }

    [Fact]
    public async Task OnPostCreateDeleteAndMove_CallExpectedUseCases()
    {
        // Arrange
        User currentUser = TestUsers.Create();
        CreateMoneyMovementRequest createRequest = new() { AccountId = Guid.NewGuid(), Amount = 100 };
        DeleteMoneyMovementRequest deleteRequest = new() { MoneyMovementId = Guid.NewGuid() };
        MoveMoneyMovementToAccountRequest moveRequest = new() { MoneyMovementId = Guid.NewGuid(), TargetAccountId = Guid.NewGuid() };

        Dependencies dependencies = new();
        dependencies.Create
            .Setup(useCase => useCase.ExecuteAsync(createRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MoneyMovementDto>.Ok(new MoneyMovementDto { Id = Guid.NewGuid() }));
        dependencies.Delete
            .Setup(useCase => useCase.ExecuteAsync(deleteRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        dependencies.Move
            .Setup(useCase => useCase.ExecuteAsync(moveRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        // Act
        JsonResult create = await model.OnPostCreateAsync(createRequest, CancellationToken.None);
        JsonResult delete = await model.OnPostDeleteAsync(deleteRequest, CancellationToken.None);
        JsonResult move = await model.OnPostMoveToAccountAsync(moveRequest, CancellationToken.None);

        // Assert
        JsonResultAssert.Data<MoneyMovementDto>(create);
        JsonResultAssert.Success(delete);
        JsonResultAssert.Success(move);
        dependencies.Create.VerifyAll();
        dependencies.Delete.VerifyAll();
        dependencies.Move.VerifyAll();
    }

    [Fact]
    public async Task OnPostLinkAndUnlinkReceipt_CallExpectedUseCases()
    {
        // Arrange
        User currentUser = TestUsers.Create();
        LinkMoneyMovementReceiptRequest linkRequest = new() { MoneyMovementId = Guid.NewGuid(), ReceiptId = Guid.NewGuid() };
        UnlinkMoneyMovementReceiptRequest unlinkRequest = new() { MoneyMovementId = linkRequest.MoneyMovementId, ReceiptId = linkRequest.ReceiptId };

        Dependencies dependencies = new();
        dependencies.Link
            .Setup(useCase => useCase.ExecuteAsync(linkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        dependencies.Unlink
            .Setup(useCase => useCase.ExecuteAsync(unlinkRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        // Act
        JsonResult link = await model.OnPostLinkReceiptAsync(linkRequest, CancellationToken.None);
        JsonResult unlink = await model.OnPostUnlinkReceiptAsync(unlinkRequest, CancellationToken.None);

        // Assert
        JsonResultAssert.Success(link);
        JsonResultAssert.Success(unlink);
        dependencies.Link.VerifyAll();
        dependencies.Unlink.VerifyAll();
    }

    [Fact]
    public async Task OnPostPreviewImportAndImport_CallExpectedUseCases()
    {
        // Arrange
        User currentUser = TestUsers.Create();
        Guid accountId = Guid.NewGuid();
        SaveBankStatementImportRequest importRequest = new() { AccountId = accountId };
        FormFile file = new(new MemoryStream([1, 2, 3]), 0, 3, "file", "statement.pdf");

        Dependencies dependencies = new();
        dependencies.Preview
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<PreviewBankStatementImportRequest>(request =>
                    request.BankId == "tbank" &&
                    request.AccountId == accountId &&
                    request.FileName == "statement.pdf" &&
                    request.FileLength == 3 &&
                    request.FileStream != null),
                currentUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BankStatementImportPreviewDto>.Ok(new BankStatementImportPreviewDto { BankId = "tbank", AccountId = accountId }));
        dependencies.Import
            .Setup(useCase => useCase.ExecuteAsync(importRequest, currentUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BankStatementImportResultDto>.Ok(new BankStatementImportResultDto { CreatedCount = 1 }));

        MoneyMovementsModel model = dependencies.CreateModel(currentUser);

        // Act
        JsonResult previewJson = await model.OnPostPreviewImportAsync("tbank", accountId, file, CancellationToken.None);
        JsonResult importJson = await model.OnPostImportAsync(importRequest, CancellationToken.None);

        // Assert
        BankStatementImportPreviewDto preview = JsonResultAssert.Data<BankStatementImportPreviewDto>(previewJson);
        BankStatementImportResultDto importResult = JsonResultAssert.Data<BankStatementImportResultDto>(importJson);
        Assert.Equal("tbank", preview.BankId);
        Assert.Equal(1, importResult.CreatedCount);
        dependencies.Preview.VerifyAll();
        dependencies.Import.VerifyAll();
    }

    [Fact]
    public async Task RemainingHandlers_ForwardCurrentUserAndToken_AndPreserveResults()
    {
        // Arrange
        User user = TestUsers.Create();
        Guid movementId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<UserAccountViewModel> accounts = [new()];
        List<BankStatementImportBankDto> banks = [new() { Id = "tbank" }];
        List<MoneyMovementReceiptDto> receipts = [new() { ReceiptId = Guid.NewGuid() }];
        OpenReceiptRequest open = new() { ReceiptId = receipts[0].ReceiptId };
        ReceiptDto detail = new() { Id = open.ReceiptId };
        UpdateMoneyMovementCommentRequest comment = new() { MoneyMovementId = movementId, Comment = "comment" };
        Dependencies dependencies = new();
        dependencies.GetAccounts.Setup(item => item.ExecuteAsync(user.Id, ct)).ReturnsAsync(accounts);
        dependencies.GetBanks.Setup(item => item.ExecuteAsync(ct)).ReturnsAsync(banks);
        dependencies.GetLinkedReceipts.Setup(item => item.ExecuteAsync(movementId, user.Id, ct))
            .ReturnsAsync(ServiceResult<List<MoneyMovementReceiptDto>>.Ok(receipts));
        dependencies.OpenReceipt.Setup(item => item.ExecuteAsync(open.ReceiptId, user, ct))
            .ReturnsAsync(ServiceResult<ReceiptDto>.Ok(detail));
        dependencies.UpdateComment.Setup(item => item.ExecuteAsync(comment, user.Id, ct))
            .ReturnsAsync(ServiceResult.Fail(ServiceErrorType.Conflict, "Concurrent change"));
        MoneyMovementsModel model = dependencies.CreateModel(user);

        // Act
        JsonResult accountsResult = await model.OnGetAccountsAsync(ct);
        JsonResult banksResult = await model.OnGetImportBanksAsync(ct);
        JsonResult receiptsResult = await model.OnGetLinkedReceiptsAsync(movementId, ct);
        JsonResult detailResult = await model.OnPostOpenReceiptAsync(open, ct);
        JsonResult commentResult = await model.OnPostUpdateCommentAsync(comment, ct);

        // Assert
        Assert.Same(accounts, JsonResultAssert.Data<List<UserAccountViewModel>>(accountsResult));
        Assert.Same(banks, JsonResultAssert.Data<List<BankStatementImportBankDto>>(banksResult));
        Assert.Same(receipts, JsonResultAssert.Data<List<MoneyMovementReceiptDto>>(receiptsResult));
        Assert.Same(detail, JsonResultAssert.Data<ReceiptDto>(detailResult));
        JsonResultAssert.Failure(commentResult, 409, "Concurrent change");
        dependencies.GetAccounts.VerifyAll();
        dependencies.GetBanks.VerifyAll();
        dependencies.GetLinkedReceipts.VerifyAll();
        dependencies.OpenReceipt.VerifyAll();
        dependencies.UpdateComment.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewHandler_ForwardsMissingOrEmptyFile_ToApplication(bool missing)
    {
        // Arrange
        User user = TestUsers.Create();
        Guid accountId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        IFormFile? file = null;
        if (!missing)
        {
            file = new FormFile(new MemoryStream(), 0, 0, "file", "empty.pdf");
        }

        Dependencies dependencies = new();
        dependencies.Preview.Setup(item => item.ExecuteAsync(
            It.Is<PreviewBankStatementImportRequest>(request => request.AccountId == accountId &&
                request.FileLength == 0 && (request.FileStream == null) == missing), user.Id, ct))
            .ReturnsAsync(ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, "Выберите файл выписки."));
        MoneyMovementsModel model = dependencies.CreateModel(user);

        // Act
        JsonResult result = await model.OnPostPreviewImportAsync("tbank", accountId, file!, ct);

        // Assert
        JsonResultAssert.Failure(result, 400, "Выберите файл выписки.");
        dependencies.Preview.VerifyAll();
        dependencies.Import.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewHandler_DisposesOwnedStream_AfterResultOrException(bool throws)
    {
        // Arrange
        User user = TestUsers.Create();
        Guid accountId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        FormFile file = new(new MemoryStream([1, 2, 3]), 0, 3, "file", "statement.pdf");
        Stream? captured = null;
        InvalidOperationException failure = new("PDF failure");
        Dependencies dependencies = new();
        dependencies.Preview.Setup(item => item.ExecuteAsync(It.IsAny<PreviewBankStatementImportRequest>(), user.Id, ct))
            .Returns((PreviewBankStatementImportRequest request, Guid userId, CancellationToken token) =>
            {
                captured = request.FileStream;
                Assert.NotNull(captured);
                Assert.Equal(1, captured.ReadByte());
                if (throws)
                {
                    throw failure;
                }

                return Task.FromResult(ServiceResult<BankStatementImportPreviewDto>.Ok(new()));
            });
        MoneyMovementsModel model = dependencies.CreateModel(user);

        // Act
        Exception? thrown = await Record.ExceptionAsync(() => model.OnPostPreviewImportAsync("tbank", accountId, file, ct));

        // Assert
        if (throws)
        {
            Assert.Same(failure, thrown);
        }
        else
        {
            Assert.Null(thrown);
        }

        Assert.NotNull(captured);
        Assert.Throws<ObjectDisposedException>(() => captured.ReadByte());
        dependencies.Preview.VerifyAll();
    }

    [Fact]
    public async Task ActualRegistrations_ResolvePageWithDirectUseCases_WithoutOpeningConnection()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.invalid",
            ["ProverkachekaApiToken"] = "unused-test-value"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddTransient<MoneyMovementsModel>();
        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        // Act
        MoneyMovementsModel page = scope.ServiceProvider.GetRequiredService<MoneyMovementsModel>();
        IPreviewBankStatementImportUseCase preview = scope.ServiceProvider.GetRequiredService<IPreviewBankStatementImportUseCase>();
        ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        // Assert
        Assert.NotNull(page);
        Assert.IsType<PreviewBankStatementImportUseCase>(preview);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Null(typeof(IPreviewBankStatementImportUseCase).Assembly.GetType(
            "CostVision.Application.UseCases.MoneyMovements.IMoneyMovementsPageUseCase"));
    }

    private sealed class Dependencies
    {
        public Mock<IGetMoneyMovementAccountsUseCase> GetAccounts { get; } = new(MockBehavior.Strict);
        public Mock<IGetMoneyMovementListUseCase> GetList { get; } = new(MockBehavior.Strict);
        public Mock<ICreateMoneyMovementUseCase> Create { get; } = new(MockBehavior.Strict);
        public Mock<IMoveMoneyMovementToAccountUseCase> Move { get; } = new(MockBehavior.Strict);
        public Mock<IDeleteMoneyMovementUseCase> Delete { get; } = new(MockBehavior.Strict);
        public Mock<IUpdateMoneyMovementCommentUseCase> UpdateComment { get; } = new(MockBehavior.Strict);
        public Mock<IGetBankStatementImportBanksUseCase> GetBanks { get; } = new(MockBehavior.Strict);
        public Mock<IPreviewBankStatementImportUseCase> Preview { get; } = new(MockBehavior.Strict);
        public Mock<IImportMoneyMovementsUseCase> Import { get; } = new(MockBehavior.Strict);
        public Mock<IGetLinkedMoneyMovementReceiptsUseCase> GetLinkedReceipts { get; } = new(MockBehavior.Strict);
        public Mock<IGetMoneyMovementReceiptCandidatesUseCase> GetReceiptCandidates { get; } = new(MockBehavior.Strict);
        public Mock<ILinkMoneyMovementReceiptUseCase> Link { get; } = new(MockBehavior.Strict);
        public Mock<IUnlinkMoneyMovementReceiptUseCase> Unlink { get; } = new(MockBehavior.Strict);
        public Mock<IGetReceiptWithItemsUseCase> OpenReceipt { get; } = new(MockBehavior.Strict);

        public MoneyMovementsModel CreateModel(User currentUser)
        {
            return new MoneyMovementsModel(GetAccounts.Object, GetList.Object, Create.Object, Move.Object,
                Delete.Object, UpdateComment.Object, GetBanks.Object, Preview.Object, Import.Object,
                GetLinkedReceipts.Object, GetReceiptCandidates.Object, Link.Object, Unlink.Object, OpenReceipt.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
