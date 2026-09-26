namespace PlatformVault.Domain.Objects;

/// <summary>
/// Transiciones del ciclo de vida (glosario §3.1). La eliminación lógica y la purga pertenecen a la iteración 1B.
/// </summary>
public static class LifecycleTransitions
{
    public static bool IsAllowed(LifecycleState from, LifecycleState to) => (from, to) switch
    {
        (LifecycleState.Draft, LifecycleState.Active) => true,
        (LifecycleState.Draft, LifecycleState.Deactivated) => true,
        (LifecycleState.Active, LifecycleState.Suspended) => true,
        (LifecycleState.Active, LifecycleState.Deactivated) => true,
        (LifecycleState.Suspended, LifecycleState.Active) => true,
        (LifecycleState.Suspended, LifecycleState.Deactivated) => true,
        (LifecycleState.Deactivated, LifecycleState.Active) => true,
        _ => false,
    };

    /// <summary>Estados que revocan accesos temporales y bloquean revelados (RN-009).</summary>
    public static bool RevokesAccess(LifecycleState state) =>
        state is LifecycleState.Suspended or LifecycleState.Deactivated;
}
