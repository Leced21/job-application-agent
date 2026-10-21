namespace JobApplicationAgent.Gateway.Identity;

public static class AccountOwnerIdResolver
{
    public static bool CanRegister(bool registrationAllowed, bool hasUsers, string? configuredInitialOwnerId) =>
        registrationAllowed && (hasUsers || TryReadInitialOwnerId(configuredInitialOwnerId, out _));

    public static Guid Resolve(bool hasUsers, string? configuredInitialOwnerId)
    {
        if (hasUsers) return Guid.NewGuid();
        if (TryReadInitialOwnerId(configuredInitialOwnerId, out var ownerId)) return ownerId;

        throw new InvalidOperationException(
            "Configurez Identity:InitialOwnerId avec l’UUID propriétaire existant avant de créer le premier compte.");
    }

    private static bool TryReadInitialOwnerId(string? value, out Guid ownerId) =>
        Guid.TryParse(value, out ownerId) && ownerId != Guid.Empty;
}
