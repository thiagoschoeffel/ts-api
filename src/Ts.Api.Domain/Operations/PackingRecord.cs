using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Operations;

public enum LabelPrintStatus
{
    Succeeded = 1,
    Failed = 2,
}

public sealed class PackingRecord : ITenantOwned
{
    private PackingRecord() { }

    private PackingRecord(
        Guid organizationId,
        Guid orderId,
        Guid packedBy,
        DateTimeOffset packedAt,
        string idempotencyKey,
        string snapshotJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        PackedBy = packedBy;
        PackedAt = packedAt;
        IdempotencyKey = NormalizeKey(idempotencyKey);
        SnapshotJson = NormalizeRequired(snapshotJson, 100_000, "O snapshot das etiquetas é obrigatório.");
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PackedBy { get; private set; }
    public DateTimeOffset PackedAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string SnapshotJson { get; private set; } = string.Empty;

    public static PackingRecord Create(
        Guid organizationId,
        Guid orderId,
        Guid packedBy,
        DateTimeOffset packedAt,
        string idempotencyKey,
        string snapshotJson)
    {
        if (organizationId == Guid.Empty || orderId == Guid.Empty || packedBy == Guid.Empty)
            throw new DomainException("Organização, pedido e responsável são obrigatórios para embalar.");

        return new PackingRecord(organizationId, orderId, packedBy, packedAt, idempotencyKey, snapshotJson);
    }

    internal static string NormalizeKey(string value) =>
        NormalizeRequired(value, 200, "A chave de idempotência deve possuir entre 1 e 200 caracteres.");

    internal static string NormalizeRequired(string value, int maximumLength, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength) throw new DomainException(message);
        return normalized;
    }
}

public sealed class LabelPrintAttempt : ITenantOwned
{
    private LabelPrintAttempt() { }

    private LabelPrintAttempt(
        Guid organizationId,
        Guid packingRecordId,
        Guid attemptedBy,
        DateTimeOffset attemptedAt,
        string idempotencyKey,
        string selectionJson,
        LabelPrintStatus status,
        string? errorMessage)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PackingRecordId = packingRecordId;
        AttemptedBy = attemptedBy;
        AttemptedAt = attemptedAt;
        IdempotencyKey = PackingRecord.NormalizeKey(idempotencyKey);
        SelectionJson = PackingRecord.NormalizeRequired(selectionJson, 50_000, "A seleção de etiquetas é obrigatória.");
        Status = status;
        ErrorMessage = errorMessage?.Trim();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PackingRecordId { get; private set; }
    public Guid AttemptedBy { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string SelectionJson { get; private set; } = string.Empty;
    public LabelPrintStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static LabelPrintAttempt Create(
        Guid organizationId,
        Guid packingRecordId,
        Guid attemptedBy,
        DateTimeOffset attemptedAt,
        string idempotencyKey,
        string selectionJson,
        LabelPrintStatus status,
        string? errorMessage)
    {
        if (organizationId == Guid.Empty || packingRecordId == Guid.Empty || attemptedBy == Guid.Empty)
            throw new DomainException("Organização, embalagem e responsável são obrigatórios para registrar a impressão.");
        if (!Enum.IsDefined(status)) throw new DomainException("O resultado da impressão é inválido.");
        if (status == LabelPrintStatus.Failed && string.IsNullOrWhiteSpace(errorMessage))
            throw new DomainException("Uma falha de impressão deve informar o erro.");
        if (errorMessage?.Trim().Length > 2_000) throw new DomainException("O erro da impressão é muito extenso.");

        return new LabelPrintAttempt(
            organizationId, packingRecordId, attemptedBy, attemptedAt,
            idempotencyKey, selectionJson, status, errorMessage);
    }
}
