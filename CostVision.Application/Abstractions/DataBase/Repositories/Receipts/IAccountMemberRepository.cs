using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountMemberRepository :
        ICreateItemRepository<AccountMember>,
        IDeleteItemRepository<AccountMember>,
        IGetItemByPredicateRepository<AccountMember>
    {
    }
}
