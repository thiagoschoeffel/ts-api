# TS API

API autoritativa da Sabor Santè, construída em .NET 10 e organizada como monólito modular.

> **Estado atual:** o frontend demonstrativo foi consolidado como linha de base em 4 de setembro de 2026 e a implementação autoritativa da API foi retomada. Evoluções posteriores do frontend seguem sem bloquear o backend.

## Estado atual

Esta primeira fatia estabelece:

- separação entre API, aplicação, domínio e infraestrutura;
- PostgreSQL como persistência transacional;
- fundação SaaS com banco e schema compartilhados e isolamento por `OrganizationId`;
- organizações, usuários de plataforma e associações de usuário a organizações;
- autenticação OIDC/OAuth 2.0 com Authorization Code + PKCE no frontend e validação JWT na API;
- tenant ativo validado contra associação persistida, sem fallback de desenvolvimento e sem `OrganizationId` em payloads;
- políticas de leitura, operação e administração derivadas do papel da associação;
- domínio inicial de congelados (configuração, lote, validade e movimentação);
- política central de validade de 90 dias corridos usando `DateOnly`;
- fontes autoritativas mínimas de Oferta e Item Produzível;
- criação de Configuração de Congelado com apresentação e preço variável próprios;
- entrada atômica de lote + `EntradaProducao`, protegida por `Idempotency-Key`;
- consultas de gestão para configurações, estoque vendável, vencimentos, lote e movimentos;
- ajuste manual e descarte transacionais, auditáveis e idempotentes, sem edição direta de saldo;
- snapshot de nome e apresentação gravado no lote para manter etiquetas históricas estáveis;
- primeira fatia autoritativa de confirmação de Pedido, com status, versão otimista, capacidade diária, cobrança e alocação FEFO de congelados;
- confirmação transacional serializável e idempotente, com movimentos e alocações rastreáveis por PedidoItem;
- criação, edição e consulta de Pedidos abertos com itens diários e congelados validados contra fontes autoritativas;
- snapshots de oferta, item produzível, apresentação e preço preservados nos itens do Pedido;
- configuração e consulta da capacidade diária, com saldo derivado, versão otimista e unicidade por Organização/data;
- composição produzível versionada e snapshot dos componentes efetivos na confirmação;
- restrições alimentares estruturadas validadas contra os marcadores da composição;
- aquisições de plano com ledger e consumo compatível por FIFO, rastreado por item e aquisição;
- ledger de crédito financeiro, desconto auditado, taxa preservada e cobrança somente do saldo restante;
- auditoria imutável das condições e efeitos comerciais da confirmação;
- auditoria transacional de ações críticas com ator confiável, tenant, instante e correlação;
- matriz explícita de transições operacionais do Pedido, com versão otimista, idempotência e trilha histórica;
- projeções autenticadas para lista, detalhe com efeitos históricos e contexto autoritativo de montagem do Pedido;
- reagendamento atômico de Pedido confirmado, transferindo a reserva somente quando a nova data possui capacidade;
- cancelamento com liberação de capacidade apenas antes da produção, estorno dos créditos nas aquisições de origem, devolução do crédito financeiro e cancelamento de cobranças pendentes;
- destinação rastreável de congelados cancelados: retorno ao mesmo lote antes da separação, conferência humana completa após separação e quarentena/descarte depois da expedição;
- health checks de processo e banco;
- testes unitários das invariantes já implementadas;
- execução local e imagem de deploy com Docker.

Endpoints de escrita disponíveis nesta primeira fatia:

```text
POST /api/catalog/offers
POST /api/production/items
POST /api/production/items/{producibleItemId}/compositions
POST /api/customers/{customerId}/dietary-restrictions
POST /api/plans/acquisitions
POST /api/financial-credits
POST /api/frozen-stock/configurations
PUT  /api/frozen-stock/configurations/{configurationId}
GET  /api/frozen-stock
GET  /api/frozen-stock/expiration?manufacturedOn={date}
GET  /api/frozen-stock/lots/{lotId}
POST /api/frozen-stock/production-entries
POST /api/frozen-stock/lots/{lotId}/movements
POST /api/orders
GET  /api/orders
GET  /api/orders/authoring-context?operationalDate={date}
PUT  /api/orders/{orderId}
GET  /api/orders/{orderId}
PUT  /api/daily-capacities/{operationalDate}
GET  /api/daily-capacities/{operationalDate}
POST /api/orders/{orderId}/confirmation
POST /api/orders/{orderId}/status-transitions
POST /api/orders/{orderId}/rescheduling
POST /api/orders/{orderId}/cancellation
GET  /api/session
```

Todos os endpoints sob `/api` exigem `Authorization: Bearer <token>`. O token precisa ter audiência `ts-api`, subject (`sub`) correspondente a um usuário ativo da plataforma e a claim `organization_id`. Para solicitar outra associação do mesmo usuário, o shell envia `X-Organization-Id`; a API só aceita o valor depois de confirmar usuário, Organização e associação ativos no banco. O header é uma solicitação de seleção, nunca autoridade de isolamento.

Leituras aceitam qualquer associação ativa. Operações de Pedido e estoque aceitam `Owner`, `Administrator` e `Operator`; `DeliveryDriver` fica restrito a leituras até a integração logística do E13. Configuração de Catálogo, Produção, capacidade, Planos, restrições e Financeiro exige `Owner` ou `Administrator`. O `ActorId` não faz mais parte dos corpos HTTP: a autoria é sempre o usuário de plataforma resolvido pelo `sub` autenticado.

A entrada de produção, o ajuste/descarte de congelados, a criação/edição do Pedido, a configuração de capacidade, a confirmação e todas as operações de ciclo exigem `Idempotency-Key`. Edição, configuração, confirmação, transição, reagendamento e cancelamento também exigem `ExpectedVersion` e rejeitam alterações concorrentes. Uma repetição só devolve o efeito persistido quando recurso, versão original e conteúdo coincidem; reutilizar a chave para outra intenção gera conflito.

No Pedido, a modalidade vem da Oferta ativa. Itens diários exigem o Item Produzível escolhido e recebem o preço informado para o rascunho; itens congelados rejeitam preço enviado pelo cliente e usam o preço da Configuração de Congelado ativa. Na confirmação, a versão mais recente da composição é consolidada no Pedido e validada contra as restrições do cliente. Esses snapshots permanecem estáveis mesmo que a composição mude depois. O `CustomerId` continua sendo uma identidade externa obrigatória até o domínio autoritativo de Clientes do E12.

O corpo da confirmação pode solicitar créditos por `OrderItemId`, desconto com motivo, taxa de entrega e crédito financeiro. Créditos de plano são consumidos das aquisições compatíveis mais antigas; cada crédito cobre no máximo o benefício contratado e eventuais upgrades continuam no saldo financeiro. Desconto não é pagamento, crédito financeiro não é crédito de plano, e a cobrança registra somente o saldo final positivo.

O reagendamento autoritativo é permitido somente em `Confirmed`, antes do início da produção. A transação valida a capacidade da nova data antes de liberar a reserva anterior; itens congelados continuam ligados aos lotes já alocados. O cancelamento deriva o estágio do status persistido, registra motivo, ator e instante e não aceita que o cliente declare um estágio arbitrário. Antes da produção, `CommercialDisposition` deve ser `Reverse`; depois do início, o operador precisa decidir explicitamente entre `Reverse` e `Preserve`, pois cancelamento operacional não implica reembolso automático.

Quando há congelados, `FrozenDisposition` explicita o destino físico. Em `Confirmed`, unidades ainda não separadas devem usar `ReturnToStock` e geram `OrderReversal` no mesmo lote. Em `InProduction` ou `InPacking`, o retorno exige `FrozenReturnInspection` com embalagem, temperatura e rastreabilidade íntegras. Em `InDelivery` ou `DeliveryFailed`, retorno ao estoque vendável é proibido; deve-se registrar `Quarantine` ou `Discarded`. Como a saída já ocorreu na confirmação, quarentena e descarte são destinos físicos auditados e não debitam o lote uma segunda vez.

O provedor escolhido é o Keycloak, configurado como servidor OIDC substituível por outro emissor compatível. O `compose.yaml` fixa a versão local e importa um realm mínimo; produção deve usar HTTPS, credenciais próprias, persistência administrada e os valores `Authentication:Authority`/`Audience` do ambiente. Chamadas anônimas recebem `401`; identidade desconhecida, associação ausente/inativa e tentativa cross-tenant recebem `403`.

## Executar com Docker

```bash
cp .env.example .env
docker compose up --build
```

A API fica em `http://localhost:8080` e o Keycloak local em `http://localhost:8081`. O realm de desenvolvimento cria `admin@saborsante.local` com senha temporária `change-me`; altere-a no primeiro login. Verificações:

```text
GET /health/live
GET /health/ready
GET /openapi/v1.json (ambiente Development)
GET /api/session (com Bearer token)
```

Em desenvolvimento, as migrations pendentes são aplicadas na inicialização. Em produção, devem ser executadas como uma etapa explícita e única do deploy antes de subir novas réplicas.

O PostgreSQL fica acessível apenas em `127.0.0.1:5433` por padrão, configurável por `POSTGRES_PORT`.

O shell em `http://localhost:4173` usa Authorization Code + PKCE. Configure nele `VITE_API_URL`, `VITE_OIDC_AUTHORITY` e `VITE_OIDC_CLIENT_ID`; tokens ficam em `sessionStorage`, enquanto a organização selecionada permanece apenas no estado da sessão e é revalidada pela API a cada troca.

## Executar testes

```bash
dotnet restore
dotnet test --no-restore --disable-build-servers -m:1
dotnet build --configuration Release --no-restore --disable-build-servers -m:1
```

Os testes de persistência atuais usam EF Core InMemory. Eles validam regras e mapeamentos exercitados pela suíte, mas não substituem a aplicação das migrations em PostgreSQL real.

Em ambientes Codex restritos, execute o `dotnet test` com permissão ampliada, pois o VSTest abre um socket local. Execute comandos `docker compose` da mesma forma para acessar o socket do Docker. Uma falha `SocketException (13): Permission denied` durante a inicialização do runner indica bloqueio do sandbox, não falha dos testes.

## Validar migrations

Para qualquer mudança de persistência:

1. inicie somente o serviço `postgres` do `compose.yaml`;
2. crie um banco temporário com nome exclusivo para a execução;
3. aponte `ConnectionStrings__Database` para esse banco e aplique toda a cadeia de migrations;
4. registre separadamente o resultado da suíte e o resultado da migration;
5. remova apenas o banco temporário ao terminar.

Nunca limpe nem remova o volume `postgres-data` para validar migrations. O banco persistente de desenvolvimento não deve ser usado como banco descartável de teste.

## Decisões

- PostgreSQL entra desde o início porque confirmação de Pedido e baixa FEFO exigem transação e concorrência reais.
- Redis não foi adicionado: ainda não existe um caso de uso concreto que exija cache ou coordenação distribuída. Quando existir, será incluído no `compose.yaml` e acessado por uma abstração da aplicação.
- O nome e a composição pertencem ao Item Produzível; regras comuns de venda pertencem à Oferta genérica de Congelados; apresentação e preço variável pertencem à `FrozenConfiguration`.
- Dados de negócio implementam o contrato tenant-owned. Filtros globais do EF Core isolam leituras, `SaveChanges` rejeita escritas de outro tenant e chaves estrangeiras compostas impedem referências cruzadas entre organizações.
- Keycloak é o provedor OIDC inicial; identidade externa e autorização de negócio continuam desacopladas. A API mapeia `sub` para `PlatformUser` e aplica a associação/papel persistidos localmente.
- A seleção de empresa usa a claim como padrão e pode receber `X-Organization-Id`, mas nunca confia no identificador sem validar a associação ativa.
- Eventos críticos geram `AuditEvent` no mesmo `SaveChanges`, preservando usuário de plataforma, Organização, instante UTC, recurso, ação e `X-Correlation-Id` (ou o identificador criado pelo servidor).
- Unicidades de negócio e idempotência são locais à organização. A migration multi-tenant associa os dados existentes ao tenant inicial Sabor Santè.
- A confirmação de Pedido usa transação `Serializable`, versão otimista e chave idempotente por Organização; itens congelados são alocados por validade, fabricação e ID estável.
- Composição, preço e condições comerciais usados na confirmação são snapshots históricos; restrições impedem a operação antes do commit.
- Crédito de plano tem ledger próprio e é consumido por FIFO dentro da oferta elegível; crédito financeiro possui ledger monetário separado.
- Pedidos abertos não reservam capacidade nem estoque. A capacidade expõe `TotalUnits`, `ReservedUnits` e `AvailableUnits`; sua versão avança tanto em reconfiguração quanto em reserva por confirmação.
- Cancelamento e reagendamento usam a mesma fronteira transacional serializável da confirmação. Capacidade só é liberada em `Confirmed`; após `InProduction`, o esforço já iniciado permanece reservado.
- Estornos de plano preservam a aquisição e o item de origem; estornos financeiros são novos movimentos de ledger; cobranças pendentes são canceladas sem apagar o histórico.
- A trilha `OrderLifecycleEvent` preserva estágio anterior, datas anterior/nova, versão original, motivo, ator, instante, chave idempotente, decisão comercial, destino físico e resumo das reversões.
- Impressão será um adapter de infraestrutura separado e nunca alterará lote, estoque ou Pedido.

## Sequência de implementação

1. revisar o scaffold à luz dos fluxos e contratos de interface consolidados — concluído para as fatias implementadas;
2. modelar as fontes autoritativas mínimas de Oferta e Item Produzível — concluído;
3. revisar a migration inicial e estabelecer a fundação multi-tenant — concluído;
4. implementar habilitação de congelado e entrada atômica de lote + `ProductionEntry`, com idempotência — concluído;
5. implementar a primeira fatia transacional de confirmação do Pedido, com capacidade, cobrança e alocação FEFO — concluído;
6. adicionar criação autoritativa do Pedido e configuração da capacidade diária — concluído;
7. incorporar créditos de plano, crédito financeiro, composição e restrições à transação de confirmação — concluído;
8. implementar cancelamento, reagendamento e reversões dos efeitos autoritativos — concluído;
9. integrar a Gestão de Congelados por meio de um adapter HTTP tipado e autenticado — concluído;
10. integrar os fluxos autoritativos de Pedido e capacidade — concluído.
