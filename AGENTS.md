# TS API — regras do projeto

## Marco de retomada

- O frontend demonstrativo foi formalmente consolidado como linha de base em 4 de setembro de 2026; a implementação da API está autorizada.
- Evoluções posteriores do frontend não voltam a congelar este repositório. Preserve os fluxos consolidados, mas derive contratos do estudo de caso; mocks continuam não sendo DTOs definitivos.

## Arquitetura

- Use .NET 10, C# estrito e PostgreSQL.
- Mantenha a solução como monólito modular. A API expõe transporte; Application coordena casos de uso; Domain contém regras e invariantes; Infrastructure contém EF Core e adapters externos.
- Não transforme interfaces dos mocks do frontend em contratos definitivos. Modele a partir do estudo de caso em `ts-host/docs/ESTUDO DE CASO.md`.
- Toda alteração de estado crítica deve ser transacional, idempotente quando puder ser repetida por rede e auditável quando exigido pelo domínio.
- Datas civis, como fabricação e validade, usam `DateOnly`. Instantes auditáveis usam `DateTimeOffset` em UTC.

## Domínio

- Congelados não possuem catálogo paralelo. Toda configuração referencia obrigatoriamente uma Oferta e um Item Produzível existentes.
- Nome vem de Produzíveis, regras comuns de venda vêm da Oferta genérica de Congelados e o preço variável pertence à Configuração de Congelado. O Pedido preserva esses snapshots.
- A validade de novos lotes é fabricação + 90 dias corridos e fica preservada historicamente.
- Saldo de congelado é derivado de movimentos; nunca edite saldo diretamente.
- Saídas usam FEFO de forma atômica: validade, fabricação e ID estável como desempates.
- Impressão e reimpressão não alteram Pedido, lote ou estoque.
- Confirmação do Pedido consolida capacidade, composição, créditos, financeiro e estoque numa única operação conceitual.

## Infraestrutura e validação

- Serviços externos usados localmente devem existir no `compose.yaml`.
- Mantenha o `Dockerfile` apto a deploy, com build multi-stage e execução sem root.
- Não adicione Redis, broker ou outro serviço sem um caso de uso concreto.
- Nunca grave segredos no repositório; documente variáveis em `.env.example`.
- Execute `dotnet test` e `dotnet build --configuration Release` após alterações.
- Mudanças de persistência exigem migration versionada.
