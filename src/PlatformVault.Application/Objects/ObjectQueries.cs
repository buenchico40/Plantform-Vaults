using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;

namespace PlatformVault.Application.Objects;

public sealed record GetObjectQuery(Guid ObjectId);

public sealed record SearchObjectsQuery(ObjectSearchCriteria Criteria, PageRequest Page);

public sealed record ObjectHistoryQuery(Guid ObjectId);

/// <summary>US-008: detalle filtrado por ámbito. Fuera de ámbito responde 404 (IMP-08).</summary>
public sealed class GetObjectHandler(ICurrentUser user, IClock clock, ObjectAuthorizer authorizer, IObjectRepository objects)
    : IQueryHandler<GetObjectQuery, ObjectDetail>
{
    public async Task<ObjectDetail> HandleAsync(GetObjectQuery query, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(query.ObjectId, ObjectOperation.View, ct);
        return await objects.GetDetailAsync(query.ObjectId, user.Scope(), clock.UtcNow, ct) ?? throw new NotFoundFailure("objeto");
    }
}

/// <summary>US-009/US-010: búsqueda paginada; el filtro de ámbito lo aplica la base de datos (RN-010).</summary>
public sealed class SearchObjectsHandler(ICurrentUser user, IClock clock, IObjectRepository objects)
    : IQueryHandler<SearchObjectsQuery, PagedResult<ObjectSummary>>
{
    public Task<PagedResult<ObjectSummary>> HandleAsync(SearchObjectsQuery query, CancellationToken ct)
    {
        if (query.Criteria.Text is { Length: > 200 })
            throw new ValidationFailure("El texto de búsqueda admite como máximo 200 caracteres.");
        return objects.SearchAsync(query.Criteria, query.Page, user.Scope(), clock.UtcNow, ct);
    }
}

/// <summary>US-011: historial de versiones.</summary>
public sealed class ListObjectVersionsHandler(ICurrentUser user, ObjectAuthorizer authorizer, IObjectRepository objects)
    : IQueryHandler<ObjectHistoryQuery, IReadOnlyList<ObjectVersionEntry>>
{
    public async Task<IReadOnlyList<ObjectVersionEntry>> HandleAsync(ObjectHistoryQuery query, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(query.ObjectId, ObjectOperation.View, ct);
        return await objects.ListVersionsAsync(query.ObjectId, user.Scope(), ct);
    }
}

/// <summary>US-013: historial de propietarios.</summary>
public sealed class ListOwnershipHistoryHandler(ICurrentUser user, ObjectAuthorizer authorizer, IObjectRepository objects)
    : IQueryHandler<ObjectHistoryQuery, IReadOnlyList<OwnershipChange>>
{
    public async Task<IReadOnlyList<OwnershipChange>> HandleAsync(ObjectHistoryQuery query, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(query.ObjectId, ObjectOperation.View, ct);
        return await objects.ListOwnershipHistoryAsync(query.ObjectId, user.Scope(), ct);
    }
}

/// <summary>Línea de tiempo de auditoría del objeto (matriz §4.2: Auditor, Seguridad, custodios y propietarios).</summary>
public sealed class GetObjectAuditTimelineHandler(ObjectAuthorizer authorizer, IAuditReader reader)
    : IQueryHandler<ObjectHistoryQuery, IReadOnlyList<AuditEventView>>
{
    public async Task<IReadOnlyList<AuditEventView>> HandleAsync(ObjectHistoryQuery query, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(query.ObjectId, ObjectOperation.ViewAudit, ct);
        return await reader.ListByResourceAsync("ManagedObject", query.ObjectId.ToString(), ct);
    }
}

/// <summary>Eventos de uso del objeto (RN-072).</summary>
public sealed class GetObjectUsageHandler(ObjectAuthorizer authorizer, IAuditReader reader)
    : IQueryHandler<ObjectHistoryQuery, IReadOnlyList<UsageEventView>>
{
    public async Task<IReadOnlyList<UsageEventView>> HandleAsync(ObjectHistoryQuery query, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(query.ObjectId, ObjectOperation.ViewAudit, ct);
        return await reader.ListUsageByObjectAsync(query.ObjectId, ct);
    }
}
