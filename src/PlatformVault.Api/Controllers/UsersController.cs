using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Api.Controllers;

public sealed record CreateUserRequest(
    [Required, MaxLength(100)] string UserName,
    [Required, MaxLength(200)] string DisplayName,
    [MaxLength(256)] string? Email,
    Guid? AreaId,
    Guid? ManagerUserId,
    IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(
    [Required, MaxLength(200)] string DisplayName,
    [MaxLength(256)] string? Email,
    Guid? AreaId,
    Guid? ManagerUserId,
    bool IsActive);

/// <summary>Asignación de un rol. El ámbito del contrato se deriva del área del usuario en la Fase 1.</summary>
public sealed record AssignRoleRequest([Required, MaxLength(50)] string Role);

public sealed record CreateAreaRequest([Required, MaxLength(30)] string Code, [Required, MaxLength(150)] string Name);

public sealed record SubtypeView(ObjectType Type, string Code, string DisplayName, bool HoldsValue, IReadOnlyList<string> RequiredAttributes);

[Route("api/v1/users")]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserView>> Search([FromQuery] string? q, [FromQuery] string? role, [FromQuery] string? status, [FromQuery] int? page,
        [FromQuery] int? pageSize, [FromServices] IQueryHandler<SearchUsersQuery, PagedResult<UserView>> handler, CancellationToken ct)
    {
        bool? isActive = status switch
        {
            null or "" => null,
            "Active" => true,
            "Disabled" => false,
            _ => throw new ValidationFailure("status debe ser Active o Disabled."),
        };
        return handler.HandleAsync(new SearchUsersQuery(q, role, isActive, Page(page, pageSize)), ct);
    }

    /// <summary>Selector de usuarios activos (propietarios, miembros): solo identificador y nombre.</summary>
    [HttpGet("lookup")]
    public Task<IReadOnlyList<UserLookup>> Lookup([FromQuery] string? text,
        [FromServices] IQueryHandler<LookupUsersQuery, IReadOnlyList<UserLookup>> handler, CancellationToken ct) =>
        handler.HandleAsync(new LookupUsersQuery(text), ct);

    [HttpGet("{userId:guid}")]
    public Task<UserView> Get(Guid userId, [FromServices] IQueryHandler<GetUserQuery, UserView> handler, CancellationToken ct) =>
        handler.HandleAsync(new GetUserQuery(userId), ct);

    [HttpPost]
    public async Task<ActionResult<TemporaryCredential>> Create(CreateUserRequest request,
        [FromServices] ICommandHandler<CreateUserCommand, TemporaryCredential> handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateUserCommand(request.UserName, request.DisplayName, request.Email, request.AreaId,
            request.ManagerUserId, request.Roles ?? []), ct);
        return CreatedAtAction(nameof(Get), new { userId = result.UserId }, result);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Update(Guid userId, UpdateUserRequest request,
        [FromServices] ICommandHandler<UpdateUserCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UpdateUserCommand(userId, request.DisplayName, request.Email, request.AreaId, request.ManagerUserId,
            request.IsActive), ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid userId, AssignRoleRequest request,
        [FromServices] IQueryHandler<GetUserQuery, UserView> users, [FromServices] ICommandHandler<SetUserRolesCommand, Unit> handler,
        CancellationToken ct)
    {
        var user = await users.HandleAsync(new GetUserQuery(userId), ct);
        await handler.HandleAsync(new SetUserRolesCommand(userId, [.. user.Roles, request.Role]), ct);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    public async Task<IActionResult> RevokeRole(Guid userId, string role,
        [FromServices] IQueryHandler<GetUserQuery, UserView> users, [FromServices] ICommandHandler<SetUserRolesCommand, Unit> handler,
        CancellationToken ct)
    {
        var user = await users.HandleAsync(new GetUserQuery(userId), ct);
        await handler.HandleAsync(new SetUserRolesCommand(userId, user.Roles.Where(r => !string.Equals(r, role, StringComparison.Ordinal)).ToList()), ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    public Task<TemporaryCredential> ResetPassword(Guid userId,
        [FromServices] ICommandHandler<ResetUserPasswordCommand, TemporaryCredential> handler, CancellationToken ct) =>
        handler.HandleAsync(new ResetUserPasswordCommand(userId), ct);

    [HttpPost("{userId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid userId, [FromServices] ICommandHandler<UnlockUserCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UnlockUserCommand(userId), ct);
        return NoContent();
    }
}

[Route("api/v1")]
public sealed class CatalogController : ApiControllerBase
{
    [HttpGet("areas")]
    public Task<IReadOnlyList<AreaView>> Areas([FromServices] IQueryHandler<ListAreasQuery, IReadOnlyList<AreaView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ListAreasQuery(), ct);

    [HttpPost("areas")]
    public async Task<IActionResult> CreateArea(CreateAreaRequest request, [FromServices] ICommandHandler<CreateAreaCommand, Guid> handler,
        CancellationToken ct)
    {
        var id = await handler.HandleAsync(new CreateAreaCommand(request.Code, request.Name), ct);
        return Created($"/api/v1/areas/{id}", new { id });
    }

    /// <summary>Catálogo de tipos y subtipos (RN-002).</summary>
    [HttpGet("catalog/subtypes")]
    public IReadOnlyList<SubtypeView> Subtypes() =>
        ObjectCatalog.Subtypes.Select(s => new SubtypeView(s.Type, s.Code, s.DisplayName, s.PayloadKind is not null, s.RequiredDetails)).ToList();
}
