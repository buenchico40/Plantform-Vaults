using PlatformVault.Domain.Common;
using PlatformVault.Domain.Security;

namespace PlatformVault.Domain.Objects;

/// <summary>Datos de alta de un objeto (US-001 a US-005).</summary>
public sealed record ObjectRegistration(
    ObjectType Type,
    string Subtype,
    string Name,
    string? Description,
    Criticality Criticality,
    Sensitivity Sensitivity,
    DeploymentEnvironment Environment,
    Guid AreaId,
    Guid? FunctionalOwnerId,
    Guid? TechnicalOwnerId,
    CustodyMode CustodyMode,
    DateTime? ExpirationDate,
    bool NoExpirationJustified,
    IReadOnlyDictionary<string, string>? Details,
    string? Thumbprint = null);

/// <summary>Cambio de metadatos (US-006). Tipo, subtipo, ambiente, área y custodia no cambian (RN-002, RN-005).</summary>
public sealed record MetadataChange(
    string Name,
    string? Description,
    DateTime? ExpirationDate,
    bool NoExpirationJustified,
    IReadOnlyDictionary<string, string>? Details);

/// <summary>Situación de los grupos del objeto, necesaria para activarlo (RN-104).</summary>
public sealed record GroupCoverage(int AssignedActiveGroups, int MaxActiveMembersInAGroup);

/// <summary>
/// Agregado Managed Object. Contiene las invariantes de negocio; la persistencia la hacen los
/// procedimientos almacenados a través del repositorio.
/// </summary>
public sealed class ManagedObject
{
    public const int ExpiringSoonDaysDefault = 30;

    private Dictionary<string, string> _details = new(StringComparer.Ordinal);

    // Constructor para materialización desde el repositorio.
    public ManagedObject() { }

    public Guid ObjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public ObjectType ObjectType { get; set; }
    public string Subtype { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Criticality Criticality { get; set; }
    public Sensitivity Sensitivity { get; set; }
    public DeploymentEnvironment Environment { get; set; }
    public Guid AreaId { get; set; }
    public Guid? FunctionalOwnerId { get; set; }
    public Guid? TechnicalOwnerId { get; set; }
    public LifecycleState LifecycleState { get; set; }
    public CustodyMode CustodyMode { get; set; }
    public bool HasPayload { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool NoExpirationJustified { get; set; }
    public string? Thumbprint { get; set; }
    public int CurrentVersion { get; set; }
    public byte[] RowVer { get; set; } = [];

    public IReadOnlyDictionary<string, string> Details
    {
        get => _details;
        set => _details = new Dictionary<string, string>(value, StringComparer.Ordinal);
    }

    public SubtypeDefinition Definition => ObjectCatalog.Get(ObjectType, Subtype);

    public bool IsCritical => Criticality == Criticality.Critical;

    /// <summary>La fecha de expiración proviene del archivo del certificado y no se edita (RN-016).</summary>
    public bool ExpirationFromCertificate => Thumbprint is not null;

    public static ManagedObject Register(ObjectRegistration data, Guid objectId)
    {
        var definition = ObjectCatalog.Get(data.Type, data.Subtype);
        var obj = new ManagedObject
        {
            ObjectId = objectId,
            ObjectType = data.Type,
            Subtype = definition.Code,
            AreaId = data.AreaId,
            CustodyMode = data.CustodyMode,
            FunctionalOwnerId = data.FunctionalOwnerId,
            TechnicalOwnerId = data.TechnicalOwnerId,
            LifecycleState = LifecycleState.Draft,
            CurrentVersion = 1,
            Thumbprint = data.Thumbprint,
        };
        obj.Apply(definition, data.Name, data.Description, data.Criticality, data.Sensitivity, data.Environment,
            data.ExpirationDate, data.NoExpirationJustified, data.Details);
        return obj;
    }

    /// <summary>Aplica un cambio de metadatos y devuelve los nombres de los campos modificados.</summary>
    public IReadOnlyList<string> UpdateMetadata(MetadataChange change)
    {
        EnsureNotDeactivated();
        var definition = Definition;
        var expiration = change.ExpirationDate;
        if (ExpirationFromCertificate && expiration != ExpirationDate)
            throw new DomainException(DomainErrors.InvalidValue, "La fecha de expiración de un certificado cargado se toma del archivo y no se puede editar.");

        var before = Snapshot();
        Apply(definition, change.Name, change.Description, Criticality, Sensitivity, Environment,
            expiration, change.NoExpirationJustified, change.Details);
        var after = Snapshot();
        return before.Where(kv => !Equals(kv.Value, after[kv.Key])).Select(kv => kv.Key).ToList();
    }

    /// <summary>
    /// Reclasificación (PUT /classification). Rebajar un objeto Crítico exige aprobación de Seguridad (RN-107):
    /// en la iteración 1A solo puede hacerlo directamente un usuario con rol Seguridad.
    /// </summary>
    public IReadOnlyList<string> Reclassify(Criticality criticality, Sensitivity sensitivity, bool actorIsSecurity)
    {
        EnsureNotDeactivated();
        if (IsCritical && criticality != Criticality.Critical && !actorIsSecurity)
            throw new DomainException(DomainErrors.RuleViolation, "Solo Seguridad puede rebajar la criticidad de un objeto Crítico (RN-107).");
        var before = Snapshot();
        Apply(Definition, Name, Description, criticality, sensitivity, Environment, ExpirationDate, NoExpirationJustified, _details);
        var after = Snapshot();
        return before.Where(kv => !Equals(kv.Value, after[kv.Key])).Select(kv => kv.Key).ToList();
    }

    public void ChangeState(LifecycleState target, GroupCoverage coverage)
    {
        if (!LifecycleTransitions.IsAllowed(LifecycleState, target))
            throw new DomainException(DomainErrors.InvalidTransition, $"No se permite pasar de {LifecycleState} a {target}.");

        if (target == LifecycleState.Active)
            EnsureReadyForActivation(coverage);

        LifecycleState = target;
    }

    public void AssignOwners(Guid functionalOwnerId, Guid technicalOwnerId)
    {
        EnsureNotDeactivated();
        if (functionalOwnerId == Guid.Empty || technicalOwnerId == Guid.Empty)
            throw new DomainException(DomainErrors.OwnersRequired, "El objeto necesita propietario funcional y técnico.");
        FunctionalOwnerId = functionalOwnerId;
        TechnicalOwnerId = technicalOwnerId;
    }

    /// <summary>Valida que el objeto pueda recibir un valor sensible (RN-004, IMP-18).</summary>
    public PayloadKind EnsureCanStorePayload()
    {
        EnsureNotDeactivated();
        if (CustodyMode != CustodyMode.Internal)
            throw new DomainException(DomainErrors.PayloadNotAllowed, "El objeto está en modo solo metadatos y no custodia valores.");
        return Definition.PayloadKind
            ?? throw new DomainException(DomainErrors.PayloadNotAllowed, "El subtipo no admite valor custodiado.");
    }

    public ExpirationStatus GetExpirationStatus(DateTime nowUtc, int expiringSoonDays = ExpiringSoonDaysDefault)
    {
        if (ExpirationDate is null) return ExpirationStatus.NoExpiration;
        if (ExpirationDate <= nowUtc) return ExpirationStatus.Expired;
        return ExpirationDate <= nowUtc.AddDays(expiringSoonDays) ? ExpirationStatus.ExpiringSoon : ExpirationStatus.Valid;
    }

    private void EnsureReadyForActivation(GroupCoverage coverage)
    {
        if (FunctionalOwnerId is null || TechnicalOwnerId is null)
            throw new DomainException(DomainErrors.OwnersRequired, "Para activar el objeto se necesitan propietario funcional y técnico (RN-087).");
        if (ExpirationDate is null && !NoExpirationJustified)
            throw new DomainException(DomainErrors.ExpirationRequired, "Para activar el objeto se necesita fecha de expiración o la marca «Sin vencimiento» (RN-063).");
        if ((IsCritical || Sensitivity == Sensitivity.Restricted) && coverage.MaxActiveMembersInAGroup < 2)
            throw new DomainException(DomainErrors.GroupRequirement,
                "Un objeto Crítico o Restringido debe estar asignado a un grupo activo con dos o más miembros activos para activarse (RN-104).");
    }

    private void EnsureNotDeactivated()
    {
        if (LifecycleState == LifecycleState.Deactivated)
            throw new DomainException(DomainErrors.InvalidTransition, "El objeto está desactivado; reactívelo antes de modificarlo.");
    }

    private void Apply(SubtypeDefinition definition, string name, string? description, Criticality criticality,
        Sensitivity sensitivity, DeploymentEnvironment environment, DateTime? expirationDate, bool noExpirationJustified,
        IReadOnlyDictionary<string, string>? details)
    {
        var cleanName = Guard.Required(name, "Nombre", 200);
        var cleanDescription = Guard.Optional(description, "Descripción", 1000);
        var cleanDetails = CleanDetails(definition, details);

        if (CardDataDetector.ContainsCardNumber(cleanName) || CardDataDetector.ContainsCardNumber(cleanDescription)
            || cleanDetails.Values.Any(CardDataDetector.ContainsCardNumber))
            throw new DomainException(DomainErrors.CardDataDetected, "Los metadatos no pueden contener datos de tarjeta (RN-124).");

        if (definition.RequiredCriticality is { } required && criticality != required)
            throw new DomainException(DomainErrors.InvalidValue, $"Los objetos del subtipo {definition.DisplayName} deben tener criticidad {required} (RN-019).");

        if (CustodyMode == CustodyMode.Internal && sensitivity < definition.MinimumSensitivityWithPayload)
            throw new DomainException(DomainErrors.SensitivityTooLow,
                $"Un objeto que custodia su valor no puede clasificarse por debajo de {definition.MinimumSensitivityWithPayload} (RN-004).");

        if (expirationDate is null && !noExpirationJustified && LifecycleState != LifecycleState.Draft)
            throw new DomainException(DomainErrors.ExpirationRequired, "La fecha de expiración es obligatoria sin la marca «Sin vencimiento» (RN-063).");

        Subtype = definition.Code;
        Name = cleanName;
        Description = cleanDescription;
        Criticality = criticality;
        Sensitivity = sensitivity;
        Environment = environment;
        ExpirationDate = expirationDate;
        NoExpirationJustified = expirationDate is null && noExpirationJustified;
        _details = cleanDetails;
    }

    private static Dictionary<string, string> CleanDetails(SubtypeDefinition definition, IReadOnlyDictionary<string, string>? details)
    {
        var clean = new Dictionary<string, string>(StringComparer.Ordinal);
        if (details is not null)
        {
            if (details.Count > 20)
                throw new DomainException(DomainErrors.InvalidValue, "Se admiten como máximo 20 atributos adicionales.");
            foreach (var (key, value) in details)
            {
                var k = Guard.Required(key, "Atributo", 50);
                var v = Guard.Optional(value, k, 500);
                if (v is not null) clean[k] = v;
            }
        }
        foreach (var required in definition.RequiredDetails)
        {
            if (!clean.ContainsKey(required))
                throw new DomainException(DomainErrors.InvalidValue, $"El atributo {required} es obligatorio para el subtipo {definition.DisplayName} (RN-013).");
        }
        return clean;
    }

    private Dictionary<string, object?> Snapshot() => new()
    {
        ["subtype"] = Subtype,
        ["name"] = Name,
        ["description"] = Description,
        ["criticality"] = Criticality,
        ["sensitivity"] = Sensitivity,
        ["environment"] = Environment,
        ["expirationDate"] = ExpirationDate,
        ["noExpirationJustified"] = NoExpirationJustified,
        ["details"] = string.Join('|', _details.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)),
    };
}
