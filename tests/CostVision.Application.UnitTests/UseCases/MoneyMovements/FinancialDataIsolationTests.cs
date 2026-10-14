using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class FinancialDataIsolationTests
{
    [Theory]
    [InlineData(AccountAccessRole.Owner, RoleType.User)]
    [InlineData(AccountAccessRole.Editor, RoleType.User)]
    [InlineData(AccountAccessRole.Viewer, RoleType.User)]
    [InlineData(AccountAccessRole.Viewer, RoleType.Admin)]
    public async Task ReceiptListAndDetails_ExposeOnlyVisibleAccountsAndMovementTotals(AccountAccessRole role, RoleType globalRole)
    {
        Fixture data = new(role, globalRole);
        GetReceiptListUseCase list = new(data.UnitOfWork.Object);
        ServiceResult<List<ReceiptDto>> result = await list.ExecuteAsync(data.User, data.Date, data.Date, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        ReceiptDto dto = Assert.Single(result.Data);
        Assert.Equal(data.Shared.Id, Assert.Single(dto.Accounts).Id);
        Assert.Equal("Shared", dto.AccountName);
        Assert.Equal(1, dto.MoneyMovementCount);
        Assert.Equal(40m, dto.MoneyMovementsTotalSum);

        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(service => service.UserHasAccessToReceipt(data.User, data.Receipt)).Returns(true);
        GetReceiptWithItemsUseCase details = new(data.UnitOfWork.Object, access.Object);
        ServiceResult<ReceiptDto> detail = await details.ExecuteAsync(data.Receipt.Id, data.User, CancellationToken.None);
        Assert.True(detail.Success);
        Assert.NotNull(detail.Data);
        Assert.Equal(data.Shared.Id, Assert.Single(detail.Data.Accounts).Id);
        Assert.Equal(1, detail.Data.MoneyMovementCount);
        Assert.Equal(40m, detail.Data.MoneyMovementsTotalSum);
    }

    [Theory]
    [InlineData(RoleType.User)]
    [InlineData(RoleType.Admin)]
    public async Task ReceiptAuthor_AfterMembershipRemoval_KeepsReceiptWithoutPrivateData(RoleType globalRole)
    {
        Fixture data = new(AccountAccessRole.Viewer, globalRole, ownReceipt: true);
        Assert.True(data.Shared.TryRemoveMember(data.User.Id, out _, out _));
        ServiceResult<List<ReceiptDto>> result = await new GetReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User, data.Date, data.Date, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        ReceiptDto dto = Assert.Single(result.Data);
        Assert.Empty(dto.Accounts);
        Assert.Null(dto.AccountId);
        Assert.Empty(dto.AccountName);
        Assert.Equal(0, dto.MoneyMovementCount);
        Assert.Equal(0m, dto.MoneyMovementsTotalSum);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner)]
    [InlineData(AccountAccessRole.Editor)]
    [InlineData(AccountAccessRole.Viewer)]
    public async Task ReceiptLinks_ExcludePrivateOperationAndBothComments(AccountAccessRole role)
    {
        Fixture data = new(role);
        ServiceResult<List<ReceiptMoneyMovementDto>> result = await new GetLinkedReceiptMoneyMovementsUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.Receipt.Id, data.User.Id, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        ReceiptMoneyMovementDto dto = Assert.Single(result.Data);
        Assert.Equal(data.Visible.Id, dto.MoneyMovementId);
        Assert.Equal("Shared", dto.AccountName);
        Assert.Equal("visible", dto.Comment);
        Assert.Equal("visible import", dto.ImportComment);
        Assert.Equal(40m, dto.Amount);
    }

    [Fact]
    public async Task ReverseLinksAndCandidates_ExcludePrivateReceiptAndAccountName()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt privateReceipt = TestReceiptFactory.Create(createdByUserId: data.Private.CreatedByUserId,
            dateTime: data.Date, totalSum: 40m, fiscalSign: "private");
        Assert.True(privateReceipt.TryAddAccount(data.Private, out _, out _));
        data.Receipts.Add(privateReceipt);
        data.Links.Add(TestMoneyMovementFactory.CreateLink(data.Visible, privateReceipt, data.User.Id));
        ServiceResult<List<MoneyMovementReceiptDto>> linked = await new GetLinkedMoneyMovementReceiptsUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.Visible.Id, data.User.Id, CancellationToken.None);
        Assert.True(linked.Success);
        Assert.NotNull(linked.Data);
        MoneyMovementReceiptDto dto = Assert.Single(linked.Data);
        Assert.Equal(data.Receipt.Id, dto.ReceiptId);
        Assert.Equal("Shared", dto.AccountName);

        ServiceResult<PagedListDto<MoneyMovementReceiptDto>> candidates = await new GetMoneyMovementReceiptCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { MoneyMovementId = data.Visible.Id, UseAmountFilter = false, ExcludeLinkedReceipts = false },
                data.User.Id, CancellationToken.None);
        Assert.True(candidates.Success);
        Assert.NotNull(candidates.Data.Items);
        Assert.Equal("Shared", Assert.Single(candidates.Data.Items).AccountName);

        ServiceResult<MoneyMovementListDto> movements = await new GetMoneyMovementListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User.Id, data.Date, data.Date, null, CancellationToken.None);
        Assert.True(movements.Success);
        Assert.NotNull(movements.Data.Items);
        MoneyMovementDto movementDto = Assert.Single(movements.Data.Items);
        Assert.Equal(1, movementDto.LinkedReceiptCount);
        Assert.Equal(data.Receipt.TotalSum, movementDto.LinkedReceiptsTotalSum);
    }

    [Fact]
    public async Task RemovedMovementAuthor_CannotReadListsCandidatesOrLinks()
    {
        Fixture data = new(AccountAccessRole.Viewer, ownReceipt: true);
        MoneyMovement authored = TestMoneyMovementFactory.CreateManual(userId: data.User.Id, occurredAt: data.Date);
        Assert.True(authored.TryMoveToAccount(data.Shared, out _));
        data.Movements.Add(authored);
        Assert.True(data.Shared.TryRemoveMember(data.User.Id, out _, out _));
        ServiceResult<MoneyMovementListDto> list = await new GetMoneyMovementListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User.Id, data.Date, data.Date, data.Shared.Id, CancellationToken.None);
        Assert.True(list.Success);
        Assert.NotNull(list.Data.Items);
        Assert.Empty(list.Data.Items);

        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> forward = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = data.Receipt.Id, UseAmountFilter = false, ExcludeLinkedMoneyMovements = false },
                data.User.Id, CancellationToken.None);
        Assert.True(forward.Success);
        Assert.NotNull(forward.Data.Items);
        Assert.Empty(forward.Data.Items);

        ServiceResult<PagedListDto<MoneyMovementReceiptDto>> reverse = await new GetMoneyMovementReceiptCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { MoneyMovementId = authored.Id }, data.User.Id, CancellationToken.None);
        Assert.False(reverse.Success);
        ServiceResult<List<MoneyMovementReceiptDto>> links = await new GetLinkedMoneyMovementReceiptsUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(authored.Id, data.User.Id, CancellationToken.None);
        Assert.False(links.Success);
        ServiceResult<List<ReceiptMoneyMovementDto>> receiptLinks = await new GetLinkedReceiptMoneyMovementsUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.Receipt.Id, data.User.Id, CancellationToken.None);
        Assert.True(receiptLinks.Success);
        Assert.NotNull(receiptLinks.Data);
        Assert.Empty(receiptLinks.Data);
    }

    [Fact]
    public async Task RemovedAuthor_DoesNotContributeToReceiptPageOrStoreCandidateCounts()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt ownReceipt = TestReceiptFactory.Create(createdByUserId: data.User.Id, dateTime: data.Date,
            store: data.Store, fiscalSign: "own");
        data.Receipts.Add(ownReceipt);
        MoneyMovement authored = TestMoneyMovementFactory.CreateManual(userId: data.User.Id, occurredAt: data.Date);
        Assert.True(authored.TryMoveToAccount(data.Private, out _));
        data.Movements.Add(authored);
        GetReceiptListPageUseCase page = new(data.UnitOfWork.Object);
        ServiceResult<ReceiptListDto> result = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.Items.Single(item => item.Id == ownReceipt.Id).AvailableMoneyMovementCount);
        ServiceResult<StoreReceiptListDto> store = await new GetStoreReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { StoreId = data.Store.Id }, data.User.Id, CancellationToken.None);
        Assert.True(store.Success);
        Assert.NotNull(store.Data);
        Assert.Equal(0, store.Data.Items.Single(item => item.Id == ownReceipt.Id).AvailableMoneyMovementCount);
    }

    [Fact]
    public async Task PrivateAccountFilterAndSearch_CannotDiscoverSharedReceipt()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        GetReceiptListPageUseCase page = new(data.UnitOfWork.Object);
        ServiceResult<ReceiptListDto> filtered = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, AccountId = data.Private.Id }, CancellationToken.None);
        Assert.True(filtered.Success);
        Assert.NotNull(filtered.Data);
        Assert.Empty(filtered.Data.Items);
        Assert.Equal(0, filtered.Data.TotalCount);
        Assert.Equal(0m, filtered.Data.TotalSum);
        ServiceResult<ReceiptListDto> searched = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, Search = "A private" }, CancellationToken.None);
        Assert.True(searched.Success);
        Assert.NotNull(searched.Data);
        Assert.Empty(searched.Data.Items);
    }

    [Theory]
    [InlineData(AccountAccessRole.Owner, RoleType.User)]
    [InlineData(AccountAccessRole.Editor, RoleType.User)]
    [InlineData(AccountAccessRole.Viewer, RoleType.User)]
    [InlineData(AccountAccessRole.Viewer, RoleType.Admin)]
    public async Task VisibleCandidatesAndStoreTotals_AgreeAcrossRoles(AccountAccessRole role, RoleType globalRole)
    {
        Fixture data = new(role, globalRole);
        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> forward = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = data.Receipt.Id, UseAmountFilter = false, ExcludeLinkedMoneyMovements = false },
                data.User.Id, CancellationToken.None);
        Assert.True(forward.Success);
        Assert.NotNull(forward.Data.Items);
        Assert.Equal(data.Visible.Id, Assert.Single(forward.Data.Items).MoneyMovementId);

        ServiceResult<PagedListDto<MoneyMovementReceiptDto>> reverse = await new GetMoneyMovementReceiptCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { MoneyMovementId = data.Visible.Id, UseAmountFilter = false, ExcludeLinkedReceipts = false },
                data.User.Id, CancellationToken.None);
        Assert.True(reverse.Success);
        Assert.NotNull(reverse.Data.Items);
        Assert.Equal("Shared", Assert.Single(reverse.Data.Items).AccountName);

        ServiceResult<StoreReceiptListDto> store = await new GetStoreReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { StoreId = data.Store.Id }, data.User.Id, CancellationToken.None);
        Assert.True(store.Success);
        Assert.NotNull(store.Data);
        ReceiptDto dto = Assert.Single(store.Data.Items);
        Assert.Equal(data.Shared.Id, Assert.Single(dto.Accounts).Id);
        Assert.Equal(1, dto.MoneyMovementCount);
        Assert.Equal(40m, dto.MoneyMovementsTotalSum);

        ServiceResult<MoneyMovementListDto> movements = await new GetMoneyMovementListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User.Id, data.Date, data.Date, null, CancellationToken.None);
        Assert.True(movements.Success);
        Assert.NotNull(movements.Data.Items);
        MoneyMovementDto movement = Assert.Single(movements.Data.Items);
        Assert.Equal(data.Visible.Id, movement.Id);
        Assert.Equal(1, movement.LinkedReceiptCount);
        Assert.Equal(100m, movement.LinkedReceiptsTotalSum);
    }

    [Theory]
    [InlineData(RoleType.User)]
    [InlineData(RoleType.Admin)]
    public async Task Outsider_CannotReadSharedReceiptOrMovement(RoleType globalRole)
    {
        Fixture data = new(AccountAccessRole.Viewer, globalRole);
        Assert.True(data.Shared.TryRemoveMember(data.User.Id, out _, out _));
        ServiceResult<List<ReceiptDto>> receipts = await new GetReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User, data.Date, data.Date, CancellationToken.None);
        Assert.True(receipts.Success);
        Assert.NotNull(receipts.Data);
        Assert.Empty(receipts.Data);

        Mock<IReceiptAccessVerificationService> permissiveAccess = new(MockBehavior.Strict);
        permissiveAccess.Setup(service => service.UserHasAccessToReceipt(data.User, data.Receipt)).Returns(true);
        ServiceResult<ReceiptDto> details = await new GetReceiptWithItemsUseCase(data.UnitOfWork.Object, permissiveAccess.Object)
            .ExecuteAsync(data.Receipt.Id, data.User, CancellationToken.None);
        Assert.False(details.Success);
        Assert.Equal(ServiceErrorType.Forbidden, details.Error?.Type);

        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> candidates = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = data.Receipt.Id }, data.User.Id, CancellationToken.None);
        Assert.False(candidates.Success);
        ServiceResult<List<ReceiptMoneyMovementDto>> links = await new GetLinkedReceiptMoneyMovementsUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.Receipt.Id, data.User.Id, CancellationToken.None);
        Assert.False(links.Success);
        ServiceResult<MoneyMovementListDto> movements = await new GetMoneyMovementListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User.Id, data.Date, data.Date, null, CancellationToken.None);
        Assert.True(movements.Success);
        Assert.NotNull(movements.Data.Items);
        Assert.Empty(movements.Data.Items);
    }

    [Fact]
    public async Task CandidateLinkFlags_DoNotRevealInaccessibleRelatedObjects()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt unlinked = TestReceiptFactory.Create(createdByUserId: data.Shared.CreatedByUserId,
            dateTime: data.Date, fiscalSign: "candidate");
        Assert.True(unlinked.TryAddAccount(data.Shared, out _, out _));
        MoneyMovement hidden = data.Movements.Single(movement => movement.Id != data.Visible.Id);
        data.Receipts.Add(unlinked);
        data.Links.Add(TestMoneyMovementFactory.CreateLink(hidden, unlinked, data.User.Id));
        ServiceResult<PagedListDto<MoneyMovementReceiptDto>> receipts = await new GetMoneyMovementReceiptCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { MoneyMovementId = data.Visible.Id, UseAmountFilter = false, ExcludeLinkedReceipts = false },
                data.User.Id, CancellationToken.None);
        Assert.True(receipts.Success);
        Assert.NotNull(receipts.Data.Items);
        Assert.False(receipts.Data.Items.Single(receipt => receipt.ReceiptId == unlinked.Id).IsLinkedToOtherMoneyMovement);

        MoneyMovement visible = TestMoneyMovementFactory.CreateManual(userId: data.Shared.CreatedByUserId, occurredAt: data.Date);
        Assert.True(visible.TryMoveToAccount(data.Shared, out _));
        Receipt privateReceipt = TestReceiptFactory.Create(createdByUserId: data.Private.CreatedByUserId, fiscalSign: "private");
        Assert.True(privateReceipt.TryAddAccount(data.Private, out _, out _));
        data.Movements.Add(visible);
        data.Links.Add(TestMoneyMovementFactory.CreateLink(visible, privateReceipt, data.User.Id));
        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> movements = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = data.Receipt.Id, UseAmountFilter = false, ExcludeLinkedMoneyMovements = false },
                data.User.Id, CancellationToken.None);
        Assert.True(movements.Success);
        Assert.NotNull(movements.Data.Items);
        Assert.False(movements.Data.Items.Single(movement => movement.MoneyMovementId == visible.Id).IsLinkedToOtherReceipt);
    }

    [Fact]
    public async Task ReceiptOperationFiltersAndGroupedTotals_UseVisibleLinksOnly()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt hiddenLinksOnly = TestReceiptFactory.Create(createdByUserId: data.Shared.CreatedByUserId,
            dateTime: data.Date, fiscalSign: "hiddenLinksOnly");
        Assert.True(hiddenLinksOnly.TryAddAccount(data.Shared, out _, out _));
        MoneyMovement hidden = data.Movements.Single(movement => movement.Id != data.Visible.Id);
        data.Links.Add(TestMoneyMovementFactory.CreateLink(hidden, hiddenLinksOnly, data.User.Id));
        data.Receipts.Add(hiddenLinksOnly);
        GetReceiptListPageUseCase page = new(data.UnitOfWork.Object);
        ServiceResult<ReceiptListDto> without = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, OperationFilter = "withoutOperations" }, CancellationToken.None);
        Assert.True(without.Success);
        Assert.NotNull(without.Data);
        Assert.Equal(hiddenLinksOnly.Id, Assert.Single(without.Data.Items).Id);
        Assert.Equal(100m, without.Data.TotalSum);

        ServiceResult<ReceiptListDto> with = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, OperationFilter = "withOperations" }, CancellationToken.None);
        Assert.True(with.Success);
        Assert.NotNull(with.Data);
        Assert.Equal(data.Receipt.Id, Assert.Single(with.Data.Items).Id);

        Receipt copy = TestReceiptFactory.Create(createdByUserId: data.User.Id, dateTime: data.Date);
        Assert.True(copy.TryAddAccount(data.Private, out _, out _));
        data.Links.Add(TestMoneyMovementFactory.CreateLink(hidden, copy, data.User.Id));
        data.Receipts.Add(copy);
        ServiceResult<List<ReceiptDto>> grouped = await new GetReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(data.User, data.Date, data.Date, CancellationToken.None);
        Assert.True(grouped.Success);
        Assert.NotNull(grouped.Data);
        ReceiptDto dto = grouped.Data.Single(receipt => receipt.FiscalSign == "fp");
        Assert.Equal(data.Shared.Id, Assert.Single(dto.Accounts).Id);
        Assert.Equal(1, dto.MoneyMovementCount);
        Assert.Equal(40m, dto.MoneyMovementsTotalSum);
    }

    [Fact]
    public async Task HiddenAccountLinks_DoNotEnableAccountlessCandidateRuleOrLeakToJson()
    {
        Fixture data = new(AccountAccessRole.Viewer, ownReceipt: true);
        Assert.True(data.Shared.TryRemoveMember(data.User.Id, out _, out _));
        MoneyMovement owned = TestMoneyMovementFactory.CreateManual(userId: data.User.Id, occurredAt: data.Date);
        data.Movements.Add(owned);
        GetReceiptListPageUseCase page = new(data.UnitOfWork.Object);
        ServiceResult<ReceiptListDto> result = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        ReceiptDto dto = Assert.Single(result.Data.Items);
        Assert.Equal(0, dto.AvailableMoneyMovementCount);
        Assert.DoesNotContain("HasAccountLinks", System.Text.Json.JsonSerializer.Serialize(dto), StringComparison.OrdinalIgnoreCase);

        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> candidates = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = data.Receipt.Id }, data.User.Id, CancellationToken.None);
        Assert.True(candidates.Success);
        Assert.NotNull(candidates.Data.Items);
        Assert.Empty(candidates.Data.Items);
        ServiceResult<StoreReceiptListDto> store = await new GetStoreReceiptListUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { StoreId = data.Store.Id }, data.User.Id, CancellationToken.None);
        Assert.True(store.Success);
        Assert.NotNull(store.Data);
        Assert.Equal(0, Assert.Single(store.Data.Items).AvailableMoneyMovementCount);
    }

    [Fact]
    public async Task OwnAccountlessReceipt_KeepsVisibleCandidatesAndCounts()
    {
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt own = TestReceiptFactory.Create(createdByUserId: data.User.Id, dateTime: data.Date,
            store: data.Store, fiscalSign: "accountless");
        MoneyMovement owned = TestMoneyMovementFactory.CreateManual(userId: data.User.Id, occurredAt: data.Date);
        data.Receipts.Add(own);
        data.Movements.Add(owned);
        GetReceiptListPageUseCase page = new(data.UnitOfWork.Object);
        ServiceResult<ReceiptListDto> result = await page.ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Items.Single(receipt => receipt.Id == own.Id).AvailableMoneyMovementCount);
        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> candidates = await new GetReceiptMoneyMovementCandidatesUseCase(data.UnitOfWork.Object)
            .ExecuteAsync(new() { ReceiptId = own.Id }, data.User.Id, CancellationToken.None);
        Assert.True(candidates.Success);
        Assert.NotNull(candidates.Data.Items);
        Assert.Equal(owned.Id, Assert.Single(candidates.Data.Items).MoneyMovementId);
    }

    private class Fixture
    {
        public DateTime Date { get; } = new(2026, 10, 13);
        public User User { get; }
        public Account Shared { get; }
        public Account Private { get; }
        public Store Store { get; } = TestReceiptFactory.CreateStore("Store");
        public Receipt Receipt { get; }
        public MoneyMovement Visible { get; }
        public List<Receipt> Receipts { get; } = [];
        public List<MoneyMovement> Movements { get; } = [];
        public List<MoneyMovementReceipt> Links { get; } = [];
        public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Strict);

        public Fixture(AccountAccessRole role, RoleType globalRole = RoleType.User, bool ownReceipt = false)
        {
            Store.Id = Guid.NewGuid();
            User = TestUserFactory.Create(roleType: globalRole);
            Shared = TestAccountFactory.Create(ownerUserId: role == AccountAccessRole.Owner ? User.Id : Guid.NewGuid(), name: "Shared");
            if (role != AccountAccessRole.Owner)
            {
                Assert.True(Shared.TryAddMember(User.Id, role, out _, out _));
            }

            Private = TestAccountFactory.Create(name: "A private", colorHex: "#ABCDEF");
            Receipt = TestReceiptFactory.Create(createdByUserId: ownReceipt ? User.Id : Shared.CreatedByUserId,
                dateTime: Date, store: Store);
            Assert.True(Receipt.TryAddAccount(Private, out _, out _));
            Assert.True(Receipt.TryAddAccount(Shared, out _, out _));
            Receipts.Add(Receipt);
            Visible = TestMoneyMovementFactory.CreateBankStatementImport(userId: Shared.CreatedByUserId,
                occurredAt: Date, amount: 40m, comment: "visible", importComment: "visible import");
            Assert.True(Visible.TryMoveToAccount(Shared, out _));
            MoneyMovement hidden = TestMoneyMovementFactory.CreateBankStatementImport(userId: Private.CreatedByUserId,
                occurredAt: Date, amount: 60m, comment: "private comment", importComment: "private import");
            Assert.True(hidden.TryMoveToAccount(Private, out _));
            Movements.AddRange([Visible, hidden]);
            Links.Add(TestMoneyMovementFactory.CreateLink(Visible, Receipt, User.Id));
            Links.Add(TestMoneyMovementFactory.CreateLink(hidden, Receipt, User.Id));

            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(receipts, () => Receipts);
            receipts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) => Task.FromResult(Receipts.Where(predicate.Compile()).ToList()));
            receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) => Task.FromResult(Receipts.FirstOrDefault(predicate.Compile())));
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(movements, () => Movements);
            movements.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) => Task.FromResult(Movements.Where(predicate.Compile()).ToList()));
            movements.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) => Task.FromResult(Movements.FirstOrDefault(predicate.Compile())));
            Mock<IMoneyMovementReceiptRepository> links = new(MockBehavior.Strict);
            links.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovementReceipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>> include, CancellationToken ct) => Task.FromResult(Links.Where(predicate.Compile()).ToList()));
            UnitOfWork.Setup(work => work.Receipt).Returns(receipts.Object);
            UnitOfWork.Setup(work => work.MoneyMovement).Returns(movements.Object);
            UnitOfWork.Setup(work => work.MoneyMovementReceipt).Returns(links.Object);
        }
    }
}
