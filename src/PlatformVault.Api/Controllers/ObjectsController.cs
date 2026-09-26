using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Objects;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Api.Controllers;

public sealed record CreateObjectRequest(
    [Required] ObjectType Type,
    [Required, MaxLength(50)] string Subtype,
    [Required, MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    [Required] Criticality Criticality,
    [Required] Sensitivity Sensitivity,
    [Required] DeploymentEnvironment Environment,
    [Required] Guid AreaId,
    Guid? FunctionalOwnerId,
    Guid? TechnicalOwnerId,
    CustodyMode CustodyMode,
    DateTime? ExpirationDate,
    bool NoExpirationJustified,
    Dictionary<string, string>? Attributes,
    [MaxLength(65536)] string? InitialValue,
    [MaxLength(90000)] string? KeyMaterialBase64,
    [MaxLength(90000)] string? CertificateFileBase64,
    [MaxLength(256)] string? CertificateContainerPassword);

public sealed record UpdateObjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    DateTime? ExpirationDate,
    bool NoExpirationJustified,
    Dictionary<string, string>? Attributes,
    [Required, MaxLength(500)] string Reason);

public sealed record ReclassifyRequest([Required] Criticality Criticality, [Required] Sensitivity Sensitivity,
    [Required, MaxLength(500)] string Reason);

public sealed record ChangeStateRequest([Required] StateAction Action, [Required, MaxLength(500)] string Reason);

public sealed record AssignOwnersRequest([Required] Guid FunctionalOwnerId, [Required] Guid TechnicalOwnerId,
    [Required, MaxLength(500)] string Reason);

public sealed record SetGroupsRequest([Required] IReadOnlyList<Guid> GroupIds, [Required, MaxLength(500)] string Reason);

public sealed record UpdateValueRequest(
    [MaxLength(65536)] string? NewValue,
    [MaxLength(90000)] string? KeyMaterialBase64,
    [MaxLength(90000)] string? CertificateFileBase64,
    [MaxLength(256)] string? CertificateContainerPassword,
    DateTime? NewExpirationDate,
    [Required, MaxLength(500)] string Reason);

public sealed record RevealRequest(Guid? TemporaryAccessId);

/// <summary>Contraseña con la que se protege el PKCS#12 descargado (mínimo 12, IMP-42). No aplica a las demás partes.</summary>
public sealed record DownloadRequest([MaxLength(256)] string? DownloadPassword);

[Route("api/v1/objects")]
public sealed class ObjectsController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<ObjectSummary>> Search(
        [FromQuery] string? q, [FromQuery] ObjectType? type, [FromQuery] string? subtype, [FromQuery] Criticality? criticality,
        [FromQuery] Sensitivity? sensitivity, [FromQuery] DeploymentEnvironment? environment, [FromQuery] Guid? areaId, [FromQuery] Guid? groupId,
        [FromQuery] Guid? ownerId, [FromQuery] LifecycleState? lifecycleState, [FromQuery] ExpirationStatus? expirationStatus,
        [FromQuery] DateTime? expiresFrom, [FromQuery] DateTime? expiresTo, [FromQuery] bool orphanOwner,
        [FromQuery] ObjectSortField sortBy, [FromQuery] bool sortDescending, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<SearchObjectsQuery, PagedResult<ObjectSummary>> handler, CancellationToken ct)
    {
        RejectUnsupported("applicationId", "insecureConfiguration", "noUsage", "splitKey", "includeDeleted");
        return handler.HandleAsync(new SearchObjectsQuery(new ObjectSearchCriteria
        {
            Text = q,
            Type = type,
            Subtype = string.IsNullOrWhiteSpace(subtype) ? null : subtype,
            Criticality = criticality,
            Sensitivity = sensitivity,
            Environment = environment,
            LifecycleState = lifecycleState,
            AreaId = areaId,
            GroupId = groupId,
            OwnerId = ownerId,
            ExpirationStatus = expirationStatus,
            WithoutOwner = orphanOwner,
            ExpiresAfter = expiresFrom,
            ExpiresBefore = expiresTo,
            SortBy = sortBy,
            SortDescending = sortDescending,
        }, Page(page, pageSize)), ct);
    }

    [HttpPost]
    public async Task<ActionResult<ObjectCreated>> Create(CreateObjectRequest request,
        [FromServices] ICommandHandler<CreateObjectCommand, ObjectCreated> handler, CancellationToken ct)
    {
        var value = new SecretInput
        {
            Text = request.InitialValue,
            KeyMaterial = Base64(request.KeyMaterialBase64),
            CertificateFile = Base64(request.CertificateFileBase64),
            CertificatePassword = request.CertificateContainerPassword,
        };
        var result = await handler.HandleAsync(new CreateObjectCommand(request.Type, request.Subtype, request.Name, request.Description,
            request.Criticality, request.Sensitivity, request.Environment, request.AreaId, request.FunctionalOwnerId, request.TechnicalOwnerId,
            request.CustodyMode, request.ExpirationDate, request.NoExpirationJustified, request.Attributes, value), ct);
        SetETag(result.ETag);
        return CreatedAtAction(nameof(Get), new { objectId = result.Id }, result);
    }

    [HttpGet("{objectId:guid}")]
    public async Task<ObjectDetail> Get(Guid objectId, [FromServices] IQueryHandler<GetObjectQuery, ObjectDetail> handler, CancellationToken ct)
    {
        var detail = await handler.HandleAsync(new GetObjectQuery(objectId), ct);
        SetETag(detail.ETag);
        return detail;
    }

    [HttpPatch("{objectId:guid}")]
    public Task<ObjectWriteResult> Update(Guid objectId, UpdateObjectRequest request,
        [FromServices] ICommandHandler<UpdateObjectCommand, ObjectWriteResult> handler, CancellationToken ct) =>
        Write(handler.HandleAsync(new UpdateObjectCommand(objectId, IfMatch, request.Name, request.Description, request.ExpirationDate,
            request.NoExpirationJustified, request.Attributes, request.Reason), ct));

    [HttpPut("{objectId:guid}/classification")]
    public Task<ObjectWriteResult> Reclassify(Guid objectId, ReclassifyRequest request,
        [FromServices] ICommandHandler<ReclassifyObjectCommand, ObjectWriteResult> handler, CancellationToken ct) =>
        Write(handler.HandleAsync(new ReclassifyObjectCommand(objectId, IfMatch, request.Criticality, request.Sensitivity, request.Reason), ct));

    [HttpPost("{objectId:guid}/state")]
    public async Task<StateChangeResult> ChangeState(Guid objectId, ChangeStateRequest request,
        [FromServices] ICommandHandler<ChangeObjectStateCommand, StateChangeResult> handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangeObjectStateCommand(objectId, IfMatch, request.Action, request.Reason), ct);
        SetETag(result.ETag);
        return result;
    }

    [HttpPut("{objectId:guid}/owners")]
    public Task<ObjectWriteResult> AssignOwners(Guid objectId, AssignOwnersRequest request,
        [FromServices] ICommandHandler<AssignOwnersCommand, ObjectWriteResult> handler, CancellationToken ct) =>
        Write(handler.HandleAsync(new AssignOwnersCommand(objectId, IfMatch, request.FunctionalOwnerId, request.TechnicalOwnerId, request.Reason), ct));

    [HttpPut("{objectId:guid}/groups")]
    public Task<ObjectWriteResult> SetGroups(Guid objectId, SetGroupsRequest request,
        [FromServices] ICommandHandler<SetObjectGroupsCommand, ObjectWriteResult> handler, CancellationToken ct) =>
        Write(handler.HandleAsync(new SetObjectGroupsCommand(objectId, IfMatch, request.GroupIds, request.Reason), ct));

    [HttpPut("{objectId:guid}/value")]
    public Task<ObjectWriteResult> UpdateValue(Guid objectId, UpdateValueRequest request,
        [FromServices] ICommandHandler<UpdateObjectValueCommand, ObjectWriteResult> handler, CancellationToken ct)
    {
        var value = new SecretInput
        {
            Text = request.NewValue,
            KeyMaterial = Base64(request.KeyMaterialBase64),
            CertificateFile = Base64(request.CertificateFileBase64),
            CertificatePassword = request.CertificateContainerPassword,
        };
        return Write(handler.HandleAsync(new UpdateObjectValueCommand(objectId, IfMatch, value, request.NewExpirationDate, request.Reason), ct));
    }

    /// <summary>US-016. El valor viaja solo en esta respuesta, que no se almacena en caché (RN-084).</summary>
    [HttpPost("{objectId:guid}/reveal")]
    public Task<RevealedValue> Reveal(Guid objectId, RevealRequest? request,
        [FromServices] ICommandHandler<RevealValueCommand, RevealedValue> handler, CancellationToken ct) =>
        handler.HandleAsync(new RevealValueCommand(objectId, request?.TemporaryAccessId), ct);

    [HttpPost("{objectId:guid}/download")]
    public async Task<IActionResult> Download(Guid objectId, [FromQuery, Required] DownloadPart? part, [FromQuery] Guid? temporaryAccessId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DownloadRequest? request,
        [FromServices] ICommandHandler<DownloadObjectFileCommand, DownloadedFile> handler, CancellationToken ct)
    {
        var file = await handler.HandleAsync(new DownloadObjectFileCommand(objectId, part!.Value, temporaryAccessId, request?.DownloadPassword), ct);
        Response.Headers["X-Audit-Event-Id"] = file.AuditEventId.ToString();
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{objectId:guid}/versions")]
    public Task<IReadOnlyList<ObjectVersionEntry>> Versions(Guid objectId,
        [FromServices] IQueryHandler<ObjectHistoryQuery, IReadOnlyList<ObjectVersionEntry>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ObjectHistoryQuery(objectId), ct);

    [HttpGet("{objectId:guid}/ownership-history")]
    public Task<IReadOnlyList<OwnershipChange>> OwnershipHistory(Guid objectId,
        [FromServices] IQueryHandler<ObjectHistoryQuery, IReadOnlyList<OwnershipChange>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ObjectHistoryQuery(objectId), ct);

    [HttpGet("{objectId:guid}/audit-timeline")]
    public Task<IReadOnlyList<AuditEventView>> AuditTimeline(Guid objectId,
        [FromServices] IQueryHandler<ObjectHistoryQuery, IReadOnlyList<AuditEventView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ObjectHistoryQuery(objectId), ct);

    [HttpGet("{objectId:guid}/usage-events")]
    public Task<IReadOnlyList<UsageEventView>> UsageEvents(Guid objectId,
        [FromServices] IQueryHandler<ObjectHistoryQuery, IReadOnlyList<UsageEventView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ObjectHistoryQuery(objectId), ct);

    private async Task<ObjectWriteResult> Write(Task<ObjectWriteResult> operation)
    {
        var result = await operation;
        SetETag(result.ETag);
        return result;
    }

    private static byte[]? Base64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ValidationFailure("El archivo o material de clave no está en Base64 válido.");
        }
    }
}
