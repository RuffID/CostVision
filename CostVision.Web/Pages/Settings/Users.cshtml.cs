using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Web.Mappers;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CostVision.Application.UseCases.Authorize.Roles;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Web.Abstractions.Entity;

namespace CostVision.Web.Pages.Settings
{
    [CookieAuthorize]
    [LoadUser]
    public class UsersModel(
        IGetUserListUseCase getUserListUseCase,
        IGetUserUseCase getUserUseCase,
        ICreateUserUseCase createUserUseCase,
        IUpdateUserUseCase updateUserUseCase,
        IToggleUserActiveUseCase toggleUserActiveUseCase,
        IGetRoleListUseCase getRoleListUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<IActionResult> OnGetUserListAsync([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            ServiceResult<List<UserListItemDto>> result = await getUserListUseCase.ExecuteAsync(includeInactive, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnGetUserAsync([FromQuery] Guid id, CancellationToken ct = default)
        {
            ServiceResult<UserEditDto> result = await getUserUseCase.ExecuteAsync(id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnGetRoleListAsync(CancellationToken ct = default)
        {
            ServiceResult<List<RoleDto>> result = await getRoleListUseCase.ExecuteAsync(ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostCreateAsync([FromBody] UserUpsertRequest dto, CancellationToken ct = default)
        {
            ServiceResult result = await createUserUseCase.ExecuteAsync(dto, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostUpdateAsync([FromBody] UserUpsertRequest dto, CancellationToken ct = default)
        {
            ServiceResult result = await updateUserUseCase.ExecuteAsync(dto, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostToggleActiveAsync([FromQuery] Guid id, CancellationToken ct = default)
        {
            ServiceResult<bool> result = await toggleUserActiveUseCase.ExecuteAsync(id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
