using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class PlatformOnboarding
{
    private PlatformOnboarding() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OwnerInvitationId { get; private set; }
    public string OwnerEmail { get; private set; } = string.Empty;
    public string TimeZone { get; private set; } = string.Empty;
    public string Locale { get; private set; } = string.Empty;
    public PlatformOnboardingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static PlatformOnboarding Create(Guid organizationId, Guid ownerInvitationId,
        string ownerEmail, string timeZone, string locale, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || ownerInvitationId == Guid.Empty)
            throw new DomainException("A organização e o convite do proprietário são obrigatórios.");
        var normalizedEmail = OrganizationInvitation.NormalizeEmail(ownerEmail);
        var normalizedTimeZone = Required(timeZone, 100, "O fuso horário é obrigatório.");
        var normalizedLocale = Required(locale, 20, "A localidade é obrigatória.");
        return new PlatformOnboarding
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId,
            OwnerInvitationId = ownerInvitationId, OwnerEmail = normalizedEmail,
            TimeZone = normalizedTimeZone, Locale = normalizedLocale,
            Status = PlatformOnboardingStatus.Provisioning,
            CreatedAt = now, UpdatedAt = now, Version = 1,
        };
    }

    public void MarkAwaitingOwner(DateTimeOffset now)
    {
        if (Status == PlatformOnboardingStatus.AwaitingOwner) return;
        Status = PlatformOnboardingStatus.AwaitingOwner;
        UpdatedAt = now;
        Version++;
    }

    public void MarkNeedsAttention(DateTimeOffset now)
    {
        if (Status == PlatformOnboardingStatus.NeedsAttention) return;
        Status = PlatformOnboardingStatus.NeedsAttention;
        UpdatedAt = now;
        Version++;
    }

    public void MarkActive(DateTimeOffset now)
    {
        if (Status != PlatformOnboardingStatus.AwaitingOwner)
            throw new DomainException("Somente um onboarding com proprietário convidado pode ser ativado.");
        Status = PlatformOnboardingStatus.Active;
        UpdatedAt = now;
        Version++;
    }

    public void Resume(DateTimeOffset now)
    {
        if (Status != PlatformOnboardingStatus.NeedsAttention)
            throw new DomainException("Somente um onboarding que requer atenção pode ser retomado.");
        Status = PlatformOnboardingStatus.Provisioning;
        UpdatedAt = now;
        Version++;
    }

    private static string Required(string? value, int maximumLength, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength) throw new DomainException(message);
        return normalized;
    }
}

public sealed class PlatformProvisioningOperation
{
    public const int MaximumAttempts = 5;
    private PlatformProvisioningOperation() { }

    public Guid Id { get; private set; }
    public Guid OnboardingId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public PlatformProvisioningOperationStatus Status { get; private set; }
    public string CurrentStep { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public string? LeaseOwner { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static PlatformProvisioningOperation Create(Guid onboardingId, string idempotencyKey,
        string requestFingerprint, DateTimeOffset now)
    {
        if (onboardingId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(requestFingerprint))
            throw new DomainException("Onboarding, chave idempotente e fingerprint são obrigatórios.");
        if (idempotencyKey.Trim().Length > 200) throw new DomainException("A chave idempotente excede 200 caracteres.");
        return new PlatformProvisioningOperation
        {
            Id = Guid.NewGuid(), OnboardingId = onboardingId,
            IdempotencyKey = idempotencyKey.Trim(), RequestFingerprint = requestFingerprint,
            Status = PlatformProvisioningOperationStatus.Pending,
            CurrentStep = "owner-invitation-email", NextAttemptAt = now,
            CreatedAt = now, UpdatedAt = now, Version = 1,
        };
    }

    public bool IsClaimable(DateTimeOffset now) =>
        (Status == PlatformProvisioningOperationStatus.Pending && NextAttemptAt <= now)
        || (Status == PlatformProvisioningOperationStatus.Running && LeaseUntil <= now);

    public void Claim(string leaseOwner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (!IsClaimable(now)) throw new DomainException("A operação não está disponível para processamento.");
        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseDuration <= TimeSpan.Zero)
            throw new DomainException("O lease da operação é inválido.");
        Status = PlatformProvisioningOperationStatus.Running;
        LeaseOwner = leaseOwner.Trim();
        LeaseUntil = now.Add(leaseDuration);
        NextAttemptAt = null;
        Attempts++;
        UpdatedAt = now;
        Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = PlatformProvisioningOperationStatus.Succeeded;
        LeaseOwner = null; LeaseUntil = null; NextAttemptAt = null; LastError = null;
        UpdatedAt = now; Version++;
    }

    public bool Fail(string error, DateTimeOffset now)
    {
        LastError = SanitizeError(error);
        LeaseOwner = null; LeaseUntil = null;
        var exhausted = Attempts >= MaximumAttempts;
        Status = exhausted ? PlatformProvisioningOperationStatus.NeedsAttention
            : PlatformProvisioningOperationStatus.Pending;
        NextAttemptAt = exhausted ? null : now.AddMinutes(Math.Pow(2, Math.Max(0, Attempts - 1)));
        UpdatedAt = now; Version++;
        return exhausted;
    }

    public void Retry(DateTimeOffset now)
    {
        if (Status != PlatformProvisioningOperationStatus.NeedsAttention)
            throw new DomainException("Somente uma operação que requer atenção pode ser retomada.");
        Status = PlatformProvisioningOperationStatus.Pending;
        Attempts = 0; NextAttemptAt = now; LeaseOwner = null; LeaseUntil = null; LastError = null;
        UpdatedAt = now; Version++;
    }

    private static string SanitizeError(string? error)
    {
        var value = string.IsNullOrWhiteSpace(error) ? "Falha não identificada." : error.ReplaceLineEndings(" ").Trim();
        return value.Length <= 1000 ? value : value[..1000];
    }
}

public sealed class PlatformOutboxMessage
{
    private PlatformOutboxMessage() { }

    public Guid Id { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid InvitationId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public PlatformOutboxStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public long Version { get; private set; }

    public static PlatformOutboxMessage CreateOwnerInvitation(Guid operationId, Guid invitationId,
        DateTimeOffset now)
    {
        if (operationId == Guid.Empty || invitationId == Guid.Empty)
            throw new DomainException("Operação e convite são obrigatórios na outbox.");
        return new PlatformOutboxMessage
        {
            Id = Guid.NewGuid(), OperationId = operationId, InvitationId = invitationId,
            Kind = "owner-invitation-email", Status = PlatformOutboxStatus.Pending,
            CreatedAt = now, Version = 1,
        };
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        if (Status == PlatformOutboxStatus.Delivered) return;
        Status = PlatformOutboxStatus.Delivered; DeliveredAt = now; Version++;
    }
}

public enum PlatformOnboardingStatus { Provisioning = 1, AwaitingOwner = 2, Active = 3, NeedsAttention = 4, Cancelled = 5 }
public enum PlatformProvisioningOperationStatus { Pending = 1, Running = 2, Succeeded = 3, NeedsAttention = 4 }
public enum PlatformOutboxStatus { Pending = 1, Delivered = 2 }
