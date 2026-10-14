using System.Linq.Expressions;
using System.Reflection;
using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using EFCoreLibrary.Abstractions.Entity;
using HttpClientLibrary.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage08BoundaryTests(ITestOutputHelper output)
{
    [Fact]
    public void AcceptedBoundary_PreservesContextPredicateIncludeAndGenericProjection()
    {
        // Arrange
        Type receiptRepository = typeof(IReceiptRepository);
        Type projectedRepository = typeof(IProjectedReadRepository<Receipt>);

        // Act
        Type[] bases = receiptRepository.GetInterfaces();
        MethodInfo projection = projectedRepository.GetMethod("GetProjectedItemsAsync")!;
        ParameterInfo[] parameters = projection.GetParameters();

        // Assert
        Assert.Equal(typeof(DbContext), typeof(AppDbContextBase).BaseType);
        Assert.Equal(typeof(AppDbContextBase), typeof(ApplicationContext).BaseType);
        Assert.Contains(typeof(IGetItemByPredicateRepository<Receipt, AppDbContextBase>), bases);
        Assert.Contains(typeof(IGetItemByIdRepository<Receipt, Guid, AppDbContextBase>), bases);
        Assert.Contains(projectedRepository, bases);
        Assert.True(projection.IsGenericMethodDefinition);
        Assert.Equal(typeof(Expression<Func<Receipt, bool>>), parameters[0].ParameterType);
        Assert.Equal(typeof(Func<,>), parameters[1].ParameterType.GetGenericTypeDefinition());
        Assert.Equal(typeof(IQueryable<Receipt>), parameters[1].ParameterType.GenericTypeArguments[0]);
        Assert.Equal(typeof(IQueryable<>), parameters[1].ParameterType.GenericTypeArguments[1].GetGenericTypeDefinition());
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.Equal(typeof(Task<>), projection.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), projection.ReturnType.GenericTypeArguments[0].GetGenericTypeDefinition());
        Assert.Empty(receiptRepository.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void SuppliedLibraries_ExposeUsedEntityContextAndHttpContracts_AndReportAssemblyDependencies()
    {
        // Arrange
        Assembly persistence = typeof(IEntity<Guid>).Assembly;
        Assembly transport = typeof(IHttpApiClient).Assembly;

        // Act
        PropertyInfo identity = Assert.Single(typeof(IEntity<Guid>).GetProperties());
        PropertyInfo database = typeof(IAppDbContext<ApplicationContext>).GetProperty("Database")!;
        MethodInfo save = typeof(IAppDbContext<ApplicationContext>).GetMethod("SaveChanges")!;
        MethodInfo[] posts = typeof(IHttpApiClient).GetMethods().Where(method => method.Name == "PostAsync").ToArray();

        // Assert
        Assert.Equal("Id", identity.Name);
        Assert.Equal(typeof(Guid), identity.PropertyType);
        Assert.NotNull(identity.GetMethod);
        Assert.NotNull(identity.SetMethod);
        Assert.Equal(typeof(DatabaseFacade), database.PropertyType);
        Assert.Contains(save.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(posts, method => method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2 &&
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CancellationToken)));
        Assert.Equal(new Version(0, 0, 2, 0), persistence.GetName().Version);
        Assert.Equal(new Version(1, 0, 0, 0), transport.GetName().Version);
        Assert.Contains(persistence.GetReferencedAssemblies(), name => name.Name == "Microsoft.EntityFrameworkCore");
        foreach (Assembly assembly in new[] { persistence, transport })
        {
            Assert.NotNull(assembly.FullName);
            output.WriteLine("{0}", assembly.FullName);
            foreach (AssemblyName dependency in assembly.GetReferencedAssemblies())
            {
                output.WriteLine("{0}", dependency.FullName);
            }
        }

        output.WriteLine("{0}", database);
        output.WriteLine("{0}", save);
        foreach (MethodInfo post in posts)
        {
            output.WriteLine("{0}", post);
        }
    }
}
