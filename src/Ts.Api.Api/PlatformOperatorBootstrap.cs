using Microsoft.EntityFrameworkCore;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Api;

public static class PlatformOperatorBootstrap
{
    public const string CommandName = "bootstrap-platform-operator";

    public static bool IsRequested(string[] args) => args.FirstOrDefault() == CommandName;

    public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var subject = Read(args, "subject");
        var profileValue = Read(args, "profile");
        var actor = Read(args, "actor");
        var reason = Read(args, "reason");
        if (subject is null || profileValue is null || actor is null || reason is null
            || !Enum.TryParse<PlatformOperatorProfile>(profileValue, ignoreCase: true, out var profile)
            || !Enum.IsDefined(profile))
        {
            Console.Error.WriteLine(
                "Uso: bootstrap-platform-operator --subject=<sub> --profile=<perfil> --actor=<ator> --reason=<justificativa>");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var user = await database.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ExternalSubject == subject && item.IsActive,
                cancellationToken);
        if (user is null)
        {
            Console.Error.WriteLine("A identidade ativa informada não existe na plataforma.");
            return 3;
        }

        var existing = await database.PlatformOperatorGrants
            .SingleOrDefaultAsync(item => item.UserId == user.Id && item.Profile == profile
                && item.RevokedAt == null, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var result = existing is null ? "granted" : "already-granted";
        var grant = existing ?? PlatformOperatorGrant.Create(user.Id, profile, actor, reason, now);
        if (existing is null) database.PlatformOperatorGrants.Add(grant);
        database.PlatformAuditEvents.Add(PlatformAuditEvent.Create(null, actor,
            "platform-operator.bootstrap", "PlatformUser", user.Id, result, reason, now,
            $"bootstrap-{Guid.NewGuid():N}"));
        await database.SaveChangesAsync(cancellationToken);

        Console.WriteLine(result == "granted"
            ? $"Perfil {profile} concedido ao usuário {user.Id}."
            : $"O usuário {user.Id} já possui o perfil {profile}; nenhuma duplicação foi criada.");
        return 0;
    }

    private static string? Read(IEnumerable<string> args, string name)
    {
        var prefix = $"--{name}=";
        var value = args.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(value) ? null : value[prefix.Length..].Trim();
    }
}
