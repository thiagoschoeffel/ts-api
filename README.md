# TS API

API autoritativa da plataforma TS, construída em .NET 10 e organizada como monólito modular.

> **Estado atual:** os épicos E02–E13 estão concluídos. A fundação autoritativa,
> o ciclo transacional de Pedidos e as integrações de Congelados, Pedidos e
> capacidade, Produção, Embalagem e histórico de impressão estão disponíveis.
> Catálogo, Produzíveis, Cardápios e planejamento semanal também são persistidos pela API.
> Clientes, Planos, créditos, cobranças, pagamentos e alocações agora compartilham a mesma fonte autoritativa.
> O E14 está implementado para homologação com a Meta; a validação no número de teste oficial depende das credenciais do ambiente.

## Estado atual

As fatias implementadas estabelecem:

- separação entre API, aplicação, domínio e infraestrutura;
- PostgreSQL como persistência transacional;
- fundação SaaS com banco e schema compartilhados e isolamento por `OrganizationId`;
- organizações, usuários de plataforma e associações de usuário a organizações;
- autenticação OIDC/OAuth 2.0 com Authorization Code + PKCE no frontend e validação JWT na API;
- tenant ativo validado contra associação persistida, sem fallback de desenvolvimento e sem `OrganizationId` em payloads;
- políticas de leitura, operação e administração derivadas do papel da associação;
- grants globais de plataforma separados dos papéis tenant, com capacidades retornadas pela sessão;
- classificação explícita dos endpoints entre identidade, plataforma e negócio;
- versões imutáveis de plano SaaS, habilitações e assinatura por organização;
- ativação, suspensão e reativação concorrentes e auditáveis, com bloqueio autoritativo das APIs de negócio;
- domínio inicial de congelados (configuração, lote, validade e movimentação);
- política central de validade de 90 dias corridos usando `DateOnly`;
- fontes autoritativas completas de Oferta, tipos de componente, escolhas, adicionais e Item Produzível;
- versões imutáveis das configurações comerciais e das composições produzíveis;
- cardápio diário em rascunho/publicado, disponibilidade manual e preço efetivo do dia;
- planejamento semanal persistido e derivação de rascunhos sem sobrescrever dias existentes;
- importação de cardápios com validação integral, relatório de erros e preservação dos dias existentes;
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
- diretório de clientes com múltiplos endereços, preferências, restrições e versão otimista;
- planos autoritativos com ofertas compatíveis e aquisições que preservam as condições contratadas;
- aquisições de plano com ledger e consumo compatível por FIFO, rastreado por item e aquisição;
- ledger de crédito financeiro, desconto auditado, taxa preservada e cobrança somente do saldo restante;
- pagamentos idempotentes com alocação transacional em múltiplas cobranças e excedente lançado como crédito financeiro;
- auditoria imutável das condições e efeitos comerciais da confirmação;
- auditoria transacional de ações críticas com ator confiável, tenant, instante e correlação;
- matriz explícita de transições operacionais do Pedido, com versão otimista, idempotência e trilha histórica;
- projeções autenticadas para lista, detalhe com efeitos históricos e contexto autoritativo de montagem do Pedido;
- reagendamento atômico de Pedido confirmado, transferindo a reserva somente quando a nova data possui capacidade;
- cancelamento com liberação de capacidade apenas antes da produção, estorno dos créditos nas aquisições de origem, devolução do crédito financeiro e cancelamento de cobranças pendentes;
- destinação rastreável de congelados cancelados: retorno ao mesmo lote antes da separação, conferência humana completa após separação e quarentena/descarte depois da expedição;
- consulta diária de Produção derivada de Pedidos confirmados e de seus componentes efetivos, sem incluir itens congelados;
- fila de Embalagem autoritativa, com avanço idempotente do Pedido e snapshot histórico de uma etiqueta por unidade diária e uma etiqueta externa;
- nome do cliente preservado no Pedido para que a identificação externa não dependa de alterações cadastrais posteriores;
- tentativas de impressão e reimpressão persistidas com ator, instante, seleção e resultado, sem alterar Pedido ou estoque;
- health checks de processo e banco;
- caixa de Atendimento persistente, ordenada por conversa e vinculada ao Cliente e ao Pedido mais recente quando o telefone coincide;
- integração direta com a Meta WhatsApp Cloud API, webhook validado por `X-Hub-Signature-256`, handoff humano, envio idempotente e conciliação de entrega/falha;
- franquia mensal por número comercial com reservas concorrentes, pausa da automação em 97% e bloqueio do envio gratuito ao atingir o limite;
- testes unitários das invariantes já implementadas;
- execução local e imagem de deploy com Docker.

Endpoints disponíveis atualmente:

```text
POST /api/catalog/offers
GET  /api/catalog
POST /api/catalog/offers/configured
PUT  /api/catalog/offers/{offerId}
POST /api/catalog/component-types
PUT  /api/catalog/component-types/{componentTypeId}
POST /api/catalog/addons
PUT  /api/catalog/addons/{addonId}
POST /api/production/items
GET  /api/production/items
POST /api/production/items/configured
PUT  /api/production/items/{producibleItemId}
POST /api/production/items/{producibleItemId}/compositions
GET  /api/menus?from={date}&to={date}
GET  /api/menus/{date}
PUT  /api/menus/{date}
POST /api/menus/{date}/publication
POST /api/menus/import
PUT  /api/menu-plans/{weekStart}
GET  /api/menu-plans/{weekStart}
POST /api/customers/{customerId}/dietary-restrictions
POST /api/customers
PUT  /api/customers/{customerId}
GET  /api/commerce
POST /api/plans
PUT  /api/plans/{planId}
POST /api/plans/acquisitions
POST /api/plans/acquisitions/authoritative
POST /api/plan-credit-adjustments
POST /api/financial-credits
POST /api/financial-credit-adjustments
POST /api/payments
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
GET  /api/operations/production?operationalDate={date}
GET  /api/operations/packing?operationalDate={date}
POST /api/operations/packing/{orderId}
POST /api/operations/packing/{orderId}/print-attempts
GET  /api/session
GET  /api/identity/session
POST /api/identity/invitations/accept
GET  /api/platform/access
GET  /api/platform/organizations
GET  /api/platform/organizations/{organizationId}
GET  /api/platform/saas-plans
GET  /api/platform/organizations/{organizationId}/saas-subscription
PUT  /api/platform/organizations/{organizationId}/saas-subscription
POST /api/platform/organizations/{organizationId}/activation
POST /api/platform/organizations/{organizationId}/suspension
POST /api/platform/organizations/{organizationId}/reactivation
GET  /api/platform/audit-events
GET  /api/platform/onboardings
POST /api/platform/onboardings
GET  /api/platform/onboardings/{onboardingId}
POST /api/platform/onboardings/{onboardingId}/retry
GET  /api/membership-invitations
POST /api/membership-invitations
GET  /api/attendance
PUT  /api/attendance/conversations/{conversationId}/mode
POST /api/attendance/conversations/{conversationId}/messages
POST /api/attendance/conversations/{conversationId}/messages/{messageId}/retry
GET  /webhooks/whatsapp/{organizationId}
POST /webhooks/whatsapp/{organizationId}
```

Todos os endpoints sob `/api` exigem `Authorization: Bearer <token>`. O token precisa ter audiência `ts-api` e subject (`sub`) correspondente a um usuário ativo da plataforma. Endpoints de negócio também exigem organização ativa, assinatura SaaS com `business.access` e seleção pela claim `organization_id` ou por `X-Organization-Id`; identidade e plataforma não aceitam um tenant artificial. Para solicitar outra associação do mesmo usuário, o shell envia `X-Organization-Id`; a API só aceita o valor depois de confirmar usuário, Organização e associação ativos no banco. O header é uma solicitação de seleção, nunca autoridade de isolamento.

`GET /api/identity/session` é o contrato canônico de bootstrap; `GET /api/session` permanece como alias de compatibilidade. A resposta inclui associações e os perfis/capacidades globais derivados de grants ativos no banco. Papéis `Owner`/`Administrator` não concedem capacidades da plataforma.

O primeiro grant global é criado fora do frontend, após aplicar as migrations e confirmar que o `sub` já corresponde a um usuário ativo:

```bash
dotnet run --project src/Ts.Api.Api -- bootstrap-platform-operator \
  --subject=<subject-oidc> \
  --profile=PlatformAdministrator \
  --actor=<identificação-operacional> \
  --reason=<justificativa>
```

O comando é idempotente para usuário/perfil ativo e registra o resultado em `platform_audit_events`.
Produção deve executá-lo em ambiente administrativo controlado. Além do grant, todas as policies
de plataforma exigem evidência de MFA no token: por padrão, a claim `amr` precisa conter `mfa`.
Provedores com convenção diferente devem configurar `Authentication:PlatformMfaClaimType` e
`Authentication:PlatformMfaClaimValue`; nunca use um valor que também seja emitido em login de
fator único.

Convites de membros são enviados pelo Resend. Configure `Resend:ApiKey`, `Resend:From` com um remetente de domínio verificado e `Resend:InvitationUrl` apontando para `/convites/aceitar` no host. A chave nunca é exposta ao frontend. O token é enviado apenas no link e somente seu hash SHA-256 é persistido; o aceite autenticado exige o mesmo claim `email`, é de uso único e cria a associação de forma transacional.

O onboarding global exige a capacidade `platform.onboarding.manage` e `Idempotency-Key`. A criação persiste empresa em provisionamento, convite do primeiro `Owner`, operação, outbox e auditoria na mesma transação e responde `202` com `onboardingId` e `operationId`. Um worker reivindica operações com lease de dois minutos, retoma leases vencidos após reinício e aplica backoff exponencial por até cinco tentativas; depois disso a operação fica em `NeedsAttention` e pode ser retomada pelo endpoint de retry com `expectedVersion`. O Resend recebe como chave idempotente o ID do convite, portanto uma queda depois do envio não duplica a mensagem. Configure `Onboarding:InvitationTokenSecret` por `ONBOARDING_INVITATION_TOKEN_SECRET` com ao menos 32 caracteres; o token é derivado por HMAC e somente seu hash permanece no convite.

O catálogo inicial de plano SaaS contém a versão imutável `complete` v1. A migration atribui essa versão às organizações ativas preexistentes que possuam uma associação ativa, preservando seu acesso. Novas organizações recebem o plano explicitamente no detalhe administrativo. Ativação exige plano com `business.access`, proprietário ativo e provisionamento obrigatório concluído; suspensão mantém vínculos e dados, mas falha fechada no middleware de negócio. Todos os comandos exigem a versão esperada e registram ator, correlação e motivo na auditoria global.

Leituras aceitam qualquer associação ativa. Operações de Pedido, estoque e planejamento logístico aceitam `Owner`, `Administrator` e `Operator`; o registro de tentativa também aceita uma associação `DeliveryDriver`, sempre dentro da Organização ativa. Configuração de Catálogo, Produção, capacidade, Planos, restrições, Financeiro e cadastro de entregadores exige `Owner` ou `Administrator`. O `ActorId` não faz mais parte dos corpos HTTP: a autoria é sempre o usuário de plataforma resolvido pelo `sub` autenticado.

## Logística

`GET /api/logistics` consolida entregadores, Pedidos aptos, rotas, paradas,
tentativas e reagendamentos do tenant. Entregadores são versionados e separados
entre ativo e disponível. Rotas capturam snapshots de cliente, telefone e
endereço, preservam a ordem manual das paradas e só iniciam após revalidar todos
os Pedidos de forma transacional. Cada tentativa é histórica e idempotente;
falhas exigem motivo e podem ser reagendadas sem reescrever a tentativa anterior.

## Atendimento e WhatsApp

A integração escolhida é a Meta WhatsApp Cloud API direta. O frontend recebe somente DTOs de Atendimento; `AccessToken`, `AppSecret` e token de verificação ficam exclusivamente no processo da API. O endpoint público de webhook valida a organização configurada e a assinatura HMAC antes de persistir. O identificador externo é único por tenant, de modo que a repetição do mesmo evento não duplica mensagem nem efeito. A sequência também é única por conversa e é atribuída dentro de transação serializável.

Cada empresa configura sua própria conexão WhatsApp pela administração global em
`/api/platform/organizations/{id}/integrations`. A conexão persiste os ativos externos verificados,
estado e saúde; os três segredos ficam em registros separados, cifrados por AES-256-GCM com
`IntegrationSecrets:EncryptionKey` (`INTEGRATION_SECRETS_ENCRYPTION_KEY`, 32 bytes em Base64) e
nunca retornam ao navegador. Cadastre na Meta a URL HTTPS pública exibida pela plataforma,
`https://<api>/webhooks/whatsapp/<connection-id>`, e assine o campo `messages`. O servidor resolve a
organização pela conexão e pela assinatura; nenhum `organizationId` do payload concede autoridade.
O cadastro confirma o `phone_number_id` na Graph API antes de ativar a conexão. Limite mensal e
margem de pausa da automação são configurados por conexão e preservados no snapshot de cada período.

As variáveis legadas `WHATSAPP_ORGANIZATION_ID`, `WHATSAPP_PHONE_NUMBER_ID`,
`WHATSAPP_BUSINESS_PHONE_NUMBER`, `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_APP_SECRET` e
`WHATSAPP_WEBHOOK_VERIFY_TOKEN` são aceitas apenas para migração. Quando todas estiverem presentes
e ainda não existir conexão WhatsApp para a organização, a inicialização cria uma conexão cifrada e
auditada de forma idempotente. Depois da confirmação, remova as variáveis legadas; novos ambientes
devem cadastrar a conexão pela plataforma.

A API nunca inicia uma conversa: envia texto apenas em uma conversa previamente criada por mensagem recebida. Respostas observadas por `message_echoes` colocam a conversa em modo Humano. A coexistência com o aplicativo WhatsApp Business depende da elegibilidade e do onboarding oficial da conta; valide o espelhamento no número de teste e depois no número comercial antes do go-live.

O período de franquia é histórico e mensal por número. O uso operacional é `Delivered + Reserved`; a reserva ocorre antes da chamada à Meta, a confirmação `delivered` transforma reserva em consumo e a falha definitiva libera a reserva. Os defaults de 1.000 mensagens gratuitas e pausa em 970 refletem a política anunciada para 1º de outubro de 2026, mas são configuração operacional e precisam ser reconfirmados na [página oficial de preços](https://whatsappbusiness.com/products/platform-pricing/) antes da produção.

Privacidade e opt-in: o negócio deve informar o uso do canal e conservar a evidência do consentimento aplicável fora do texto livre da conversa; pedidos de exclusão devem seguir a política de retenção da organização. Não registre tokens nem payloads integrais em logs. Mensagens livres só podem ser respondidas dentro da janela permitida pela Meta; este fluxo não envia templates nem campanhas e não deve ser usado para marketing.
Uma rota é concluída quando todas as paradas foram tratadas, mesmo com falhas.

A entrada de produção, o ajuste/descarte de congelados, a criação/edição do Pedido, a configuração de capacidade, a confirmação, a embalagem, o registro de impressão e todas as operações de ciclo exigem `Idempotency-Key`. Edição, configuração, confirmação, transição, reagendamento, cancelamento e embalagem também exigem `ExpectedVersion` e rejeitam alterações concorrentes. Uma repetição só devolve o efeito persistido quando recurso, versão original e conteúdo coincidem; reutilizar a chave para outra intenção gera conflito.

No Pedido, a modalidade vem da Oferta ativa. Itens diários aceitam somente Oferta e Item Produzível disponíveis no Cardápio publicado da data, e o preço informado precisa coincidir com o preço efetivo publicado. Itens congelados rejeitam preço enviado pelo cliente e usam o preço da Configuração de Congelado ativa. Na confirmação, a versão mais recente da composição é consolidada no Pedido e validada contra as restrições do cliente. Esses snapshots permanecem estáveis mesmo que a composição mude depois. O `CustomerId` precisa referenciar um cliente ativo da mesma Organização; nome, endereço e contato relevantes continuam preservados historicamente no Pedido.

Produção agrega somente os componentes efetivos dos itens de produção diária em Pedidos confirmados ou em estágios posteriores da data consultada. Embalagem aceita Pedidos confirmados, em produção ou em embalagem, avança os estágios necessários numa transação serializável e persiste o snapshot antes de chamar a impressora. A estação envia ZPL 100 × 50 mm por Zebra Browser Print; depois registra na API o sucesso ou a falha. Reimpressões selecionam etiquetas do mesmo snapshot histórico, e a API rejeita identificadores alheios ao Pedido.

O corpo da confirmação pode solicitar créditos por `OrderItemId`, desconto com motivo, taxa de entrega e crédito financeiro. Créditos de plano são consumidos das aquisições compatíveis mais antigas; cada crédito cobre no máximo o benefício contratado e eventuais upgrades continuam no saldo financeiro. Desconto não é pagamento, crédito financeiro não é crédito de plano, e a cobrança registra somente o saldo final positivo.

O reagendamento autoritativo é permitido somente em `Confirmed`, antes do início da produção. A transação valida a capacidade da nova data antes de liberar a reserva anterior; itens congelados continuam ligados aos lotes já alocados. O cancelamento deriva o estágio do status persistido, registra motivo, ator e instante e não aceita que o cliente declare um estágio arbitrário. Antes da produção, `CommercialDisposition` deve ser `Reverse`; depois do início, o operador precisa decidir explicitamente entre `Reverse` e `Preserve`, pois cancelamento operacional não implica reembolso automático.

Quando há congelados, `FrozenDisposition` explicita o destino físico. Em `Confirmed`, unidades ainda não separadas devem usar `ReturnToStock` e geram `OrderReversal` no mesmo lote. Em `InProduction` ou `InPacking`, o retorno exige `FrozenReturnInspection` com embalagem, temperatura e rastreabilidade íntegras. Em `InDelivery` ou `DeliveryFailed`, retorno ao estoque vendável é proibido; deve-se registrar `Quarantine` ou `Discarded`. Como a saída já ocorreu na confirmação, quarentena e descarte são destinos físicos auditados e não debitam o lote uma segunda vez.

O provedor escolhido é o Keycloak, configurado como servidor OIDC substituível por outro emissor compatível. O `compose.yaml` fixa a versão local e importa um realm mínimo; produção deve usar HTTPS, credenciais próprias, persistência administrada e os valores `Authentication:Authority`/`Audience` do ambiente. O realm deve emitir a evidência configurada somente depois de autenticação multifator; sem ela, mesmo um usuário com grant global recebe `403` nas APIs de plataforma. Chamadas anônimas recebem `401`; identidade desconhecida, associação ausente/inativa e tentativa cross-tenant recebem `403`.

## Executar com Docker

```bash
cp .env.example .env
docker compose up --build
```

A API fica em `http://localhost:8080` e o Keycloak local em `http://localhost:8081`. O realm `ts` de desenvolvimento cria `admin@saborsante.local`, usuário da primeira organização, com senha temporária `change-me`; altere-a no primeiro login. Verificações:

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

Os testes de persistência comuns usam EF Core InMemory. Os cenários de reinício e aceite A/B do
onboarding usam PostgreSQL quando `TS_API_TEST_DATABASE` aponta para um banco descartável já
autorizado para testes; sem essa variável, eles não acessam banco externo. O CI define a variável
para seu PostgreSQL efêmero, portanto não permite que esses gates sejam pulados. A aplicação
separada de toda a cadeia de migrations continua obrigatória para mudanças de persistência.

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
- Impressão usa um adapter de estação separado e nunca altera lote, estoque ou Pedido; a API persiste somente snapshot e tentativas.

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
11. integrar Produção, Embalagem e o adapter Zebra/ZPL — concluído.
12. integrar Clientes, Planos, Créditos e Financeiro — concluído.

## Observabilidade e deploy

A API emite logs JSON com escopos de correlação, devolve `X-Correlation-Id`,
centraliza erros inesperados com um `errorId` seguro e aceita falhas autenticadas
do frontend em `POST /api/telemetry/client-errors`. Liveness, readiness com
PostgreSQL e métricas Prometheus ficam em `/health/live`, `/health/ready` e
`/metrics`.

CI executa testes, build Release, aplica toda a cadeia de migrations em
PostgreSQL 17 real, constrói a imagem e guarda o publish imutável. Em deploy,
defina `ConnectionStrings__Database` e execute `./scripts/apply-migrations.sh`
uma única vez antes das novas réplicas. O procedimento completo e o rollback
estão em `../ts-host/docs/OPERACAO_V1.md`.

O `Dockerfile` também oferece o target administrativo `migrations` para
plataformas de deploy baseadas em containers. Ele restaura a versão fixada do
`dotnet-ef`, aplica a cadeia pendente e encerra; o target final padrão continua
sendo `runtime` e não contém o SDK nem ferramentas administrativas:

```bash
docker build --target migrations -t ts-api-migrations .
docker run --rm --env-file .env.production ts-api-migrations
```
