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
- Após restaurar dependências, execute `dotnet test --no-restore --disable-build-servers -m:1` e `dotnet build --configuration Release --no-restore --disable-build-servers -m:1`. A execução serial e sem build servers é o padrão neste workspace, não apenas um fallback.
- Em ambientes Codex restritos, o VSTest precisa abrir um socket local. Execute o teste já com permissão ampliada; `SocketException (13): Permission denied` na inicialização do runner é limitação do sandbox, não falha da suíte.
- Comandos `docker compose` precisam de acesso ao socket local do Docker e também devem ser executados com permissão ampliada nesses ambientes.
- Os testes atuais de persistência usam EF Core InMemory. Testes verdes não provam que uma migration é aplicável ao PostgreSQL.
- Mudanças de persistência exigem migration versionada e validação separada em PostgreSQL real. Use um banco temporário exclusivo da execução, aplique toda a cadeia de migrations e remova somente esse banco ao terminar; nunca reutilize, limpe ou remova o volume persistente do projeto para validar uma migration.

## Execução por épicos

- O checklist, a ordem e os critérios de aceite ficam em `../ts-host/docs/ROADMAP.md`.
- A execução solicitada de um épico desse roadmap autoriza explicitamente os commits, o push da branch de trabalho e a criação ou atualização do pull request que compõem sua Definition of Done.
- Antes de iniciar, verifique o estado Git de todos os repositórios afetados e preserve mudanças que não pertençam ao épico.
- Antes da primeira alteração, crie a mesma branch de trabalho em todos os repositórios afetados, no formato `feat/eNN-descricao-curta`. Nunca implemente um épico em `main` ou `master`.
- Se já houver commits do épico na branch protegida local, preserve-os criando a branch de trabalho no `HEAD` atual; não faça reset nem descarte alterações para corrigir o fluxo.
- Ao concluir, valide, atualize a documentação, crie um commit convencional e coeso por repositório afetado, publique somente as branches de trabalho e abra ou atualize o pull request. Nunca faça push direto para `main` ou `master`.
- Um épico só pode ser marcado como concluído após todos os pushes das branches e pull requests correspondentes; falha de commit, push ou criação do PR mantém o épico em andamento.
