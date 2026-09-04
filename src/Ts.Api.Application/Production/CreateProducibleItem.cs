using Ts.Api.Application.Common;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Production;

public sealed record CreateProducibleItemCommand(string Name);

public sealed record CreateProducibleItemResult(Guid Id, string Name, bool IsActive);

public interface IProducibleItemStore
{
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(ProducibleItem item, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateProducibleItemHandler(IProducibleItemStore store)
{
    public async Task<CreateProducibleItemResult> HandleAsync(
        CreateProducibleItemCommand command,
        CancellationToken cancellationToken)
    {
        var name = command.Name?.Trim() ?? string.Empty;
        if (await store.NameExistsAsync(name, cancellationToken))
        {
            throw new ConflictException("Já existe um item produzível com esse nome.");
        }

        var item = ProducibleItem.Create(name);
        await store.AddAsync(item, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new CreateProducibleItemResult(item.Id, item.Name, item.IsActive);
    }
}
