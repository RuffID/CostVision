using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountMemberRepository : IGetItemByPredicateRepository<AccountMember>, ICreateItemRepository<AccountMember>, IDeleteItemRepository<AccountMember>
    {
    }
}