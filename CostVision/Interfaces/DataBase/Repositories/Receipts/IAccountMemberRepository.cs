using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IAccountMemberRepository : IGetItemByPredicateRepository<AccountMember>, ICreateItemRepository<AccountMember>, IDeleteItemRepository<AccountMember>
    {
    }
}