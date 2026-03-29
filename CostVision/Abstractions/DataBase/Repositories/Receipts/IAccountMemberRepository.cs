using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountMemberRepository : IGetItemByPredicateRepository<AccountMember, ApplicationContext>, ICreateItemRepository<AccountMember, ApplicationContext>, IDeleteItemRepository<AccountMember, ApplicationContext>
    {
    }
}