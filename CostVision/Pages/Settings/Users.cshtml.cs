using CostVision.Abstractions.Entity;
using CostVision.Abstractions.Service.Authorize;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Authorization;
using CostVision.Models.Dtos.Mappers;
using CostVision.Models.Requests.Authorize;
using CostVision.Models.Responses.Results;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages.Settings
{
    [CookieAuthorize]
    [LoadUser]
    public class UsersModel(IUserService userService, IRoleService roleService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<IActionResult> OnGetUserListAsync([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            ServiceResult<List<UserListItemDto>> result = await userService.GetUserListAsync(includeInactive, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnGetUserAsync([FromQuery] Guid id, CancellationToken ct = default)
        {
            ServiceResult<UserEditDto> result = await userService.GetUserAsync(id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnGetRoleListAsync(CancellationToken ct = default)
        {
            ServiceResult<List<Role>> result = await roleService.GetRoleListAsync(ct);
            if (!result.Success || result.Data == null)            
                return JsonResultMapper.ToJsonResult(result);
            
            List<RoleDto> dto = result.Data
                .OrderBy(x => x.Name)
                .Select(x => new RoleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    RoleType = x.RoleType
                })
                .ToList();

            return JsonResultMapper.ToJsonResult(ServiceResult<List<RoleDto>>.Ok(dto));
        }

        public async Task<IActionResult> OnPostCreateAsync([FromBody] UserUpsertRequest dto, CancellationToken ct = default)
        {
            ServiceResult result = await userService.CreateUserAsync(dto, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostUpdateAsync([FromBody] UserUpsertRequest dto, CancellationToken ct = default)
        {
            ServiceResult result = await userService.UpdateUserAsync(dto, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostToggleActiveAsync([FromQuery] Guid id, CancellationToken ct = default)
        {
            ServiceResult<bool> result = await userService.ToggleUserActiveAsync(id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
