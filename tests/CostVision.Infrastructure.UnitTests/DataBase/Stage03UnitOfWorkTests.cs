using System.Data;
using System.Data.Common;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.DataBase.Repositories;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Xunit;
using System.Reflection;
using Microsoft.Data.SqlClient;
using CostVision.Application.Abstractions.DataBase.Repositories;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage03UnitOfWorkTests
{
    [Fact]
    public async Task ExplicitIsolation_ReachesTransactionManager_AndSavesOnlyWhenRequested()
    {
        // Arrange
        Mock<DbTransaction> transaction = new();
        Mock<DbConnection> connection = CreateConnection(transaction);
        using ApplicationContext context = CreateContext(connection.Object);
        Mock<IUnitOfWorkContext<CostVisionContextKey>> port = new();
        port.SetupGet(item => item.Database).Returns(context.Database);
        UnitOfWorkScope work = CreateWork(port.Object);
        bool actionExecuted = false;

        // Act
        await work.ExecuteInTransaction(_ =>
        {
            actionExecuted = true;
            return Task.CompletedTask;
        }, IsolationLevel.Serializable, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(actionExecuted);
        connection.Protected().Verify("BeginDbTransactionAsync", Times.Once(), IsolationLevel.Serializable, ItExpr.IsAny<CancellationToken>());
        transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        port.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedExplicitTransaction_RollsBack_PropagatesUnknownError_AndCannotBeReused()
    {
        // Arrange
        Mock<DbTransaction> transaction = new();
        Mock<DbConnection> connection = CreateConnection(transaction);
        using ApplicationContext context = CreateContext(connection.Object);
        Mock<IUnitOfWorkContext<CostVisionContextKey>> port = new();
        port.SetupGet(item => item.Database).Returns(context.Database);
        DbUpdateException failure = new("unrelated storage error");
        port.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        UnitOfWorkScope work = CreateWork(port.Object);

        // Act
        DbUpdateException thrown = await Assert.ThrowsAsync<DbUpdateException>(() =>
            work.ExecuteInTransaction(work.SaveChangesAsync, IsolationLevel.Serializable, TestContext.Current.CancellationToken));
        InvalidOperationException reuse = await Assert.ThrowsAsync<InvalidOperationException>(() => work.SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(failure, thrown);
        Assert.Contains("scope", reuse.Message);
        transaction.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        port.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(2601, ReceiptWriteErrors.AUTHOR_IDENTITY_INDEX, true)]
    [InlineData(2627, ReceiptWriteErrors.ACCOUNT_LINK_KEY, true)]
    [InlineData(2601, "OtherConstraint", false)]
    [InlineData(547, ReceiptWriteErrors.AUTHOR_IDENTITY_INDEX, false)]
    [InlineData(-2, ReceiptWriteErrors.AUTHOR_IDENTITY_INDEX, false)]
    [InlineData(1205, "deadlock", true)]
    [InlineData(2601, "IX_Products_NormalizedName", true)]
    [InlineData(2627, "IX_Stores_NormalizedName_NormalizedAddress", true)]
    public async Task SqlFailure_MapsOnlyKnownReceiptConflicts_AndDoesNotRollbackDeadlockTwice(int number, string constraint, bool expectedConflict)
    {
        // Arrange
        Mock<DbTransaction> transaction = new();
        Mock<DbConnection> connection = CreateConnection(transaction);
        using ApplicationContext context = CreateContext(connection.Object);
        Mock<IUnitOfWorkContext<CostVisionContextKey>> port = new();
        port.SetupGet(item => item.Database).Returns(context.Database);
        string prefix = "UNIQUE KEY constraint";
        if (constraint == ReceiptWriteErrors.ACCOUNT_LINK_KEY)
        {
            prefix = "PRIMARY KEY constraint";
        }

        SqlException sql = CreateSqlException(number, prefix + " '" + constraint + "'.");
        DbUpdateException failure = new("write failed", sql);
        port.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        UnitOfWorkScope work = CreateWork(port.Object);

        // Act
        Exception? thrown = await Record.ExceptionAsync(() => work.ExecuteInTransaction(
            work.SaveChangesAsync, IsolationLevel.Serializable, TestContext.Current.CancellationToken));

        // Assert
        if (expectedConflict)
        {
            ReceiptWriteConflictException conflict = Assert.IsType<ReceiptWriteConflictException>(thrown);
            Assert.Same(failure, conflict.InnerException);
            Assert.False(string.IsNullOrWhiteSpace(conflict.Message));
        }
        else
        {
            Assert.Same(failure, thrown);
        }

        transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        if (number == 1205)
        {
            transaction.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            transaction.Verify(item => item.RollbackAsync(CancellationToken.None), Times.Once);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => work.SaveChangesAsync(TestContext.Current.CancellationToken));
        port.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    internal static SqlException CreateSqlException(int number, string message)
    {
        // Только создание синтетической ошибки SqlClient для проверки ветвления; соединение с SQL не открывается.
        ConstructorInfo constructor = typeof(SqlError).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(int), typeof(Exception)], null)
            ?? throw new InvalidOperationException("Не найден конструктор синтетической ошибки SqlClient.");
        SqlError error = (SqlError)constructor.Invoke([number, (byte)0, (byte)16, "unused", message, "unused", 0, 0, null]);
        SqlErrorCollection errors = (SqlErrorCollection)(Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)
            ?? throw new InvalidOperationException("Не создана коллекция ошибок SqlClient."));
        MethodInfo add = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Не найден метод добавления синтетической ошибки.");
        add.Invoke(errors, [error]);

        MethodInfo create = typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(SqlErrorCollection), typeof(string)], null)
            ?? throw new InvalidOperationException("Не найден метод создания синтетического исключения SqlClient.");
        return (SqlException)(create.Invoke(null, [errors, "unused"])
            ?? throw new InvalidOperationException("Не создано синтетическое исключение SqlClient."));
    }

    private static Mock<DbConnection> CreateConnection(Mock<DbTransaction> transaction)
    {
        Mock<DbConnection> connection = new();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.SetupGet(item => item.ConnectionString).Returns("Server=unused;Database=unused");
        connection.Protected().Setup<ValueTask<DbTransaction>>("BeginDbTransactionAsync",
                ItExpr.IsAny<IsolationLevel>(), ItExpr.IsAny<CancellationToken>())
            .Returns(new ValueTask<DbTransaction>(transaction.Object));
        transaction.Protected().Setup<DbConnection>("DbConnection").Returns(connection.Object);
        return connection;
    }

    private static ApplicationContext CreateContext(DbConnection connection)
    {
        return new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().UseSqlServer(connection).Options);
    }

    private static UnitOfWorkScope CreateWork(IUnitOfWorkContext<CostVisionContextKey> context)
    {
        return new UnitOfWorkScope(context);
    }
}
