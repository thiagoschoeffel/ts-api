# TS API

API autoritativa da Sabor Santè, construída em .NET 10 e organizada como monólito modular.

> **Estado atual:** o frontend demonstrativo foi consolidado como linha de base em 4 de setembro de 2026 e a implementação autoritativa da API foi retomada. Evoluções posteriores do frontend seguem sem bloquear o backend.

## Estado inicial

Esta primeira fatia estabelece:

- separação entre API, aplicação, domínio e infraestrutura;
- PostgreSQL como persistência transacional;
- fundação SaaS com banco e schema compartilhados e isolamento por `OrganizationId`;
- organizações, usuários de plataforma e associações de usuário a organizações;
- domínio inicial de congelados (configuração, lote, validade e movimentação);
- política central de validade de 90 dias corridos usando `DateOnly`;
- fontes autoritativas mínimas de Oferta e Item Produzível;
- criação de Configuração de Congelado com apresentação e preço variável próprios;
- entrada atômica de lote + `EntradaProducao`, protegida por `Idempotency-Key`;
- primeira fatia autoritativa de confirmação de Pedido, com status, versão otimista, capacidade diária, cobrança e alocação FEFO de congelados;
- confirmação transacional serializável e idempotente, com movimentos e alocações rastreáveis por PedidoItem;
- health checks de processo e banco;
- testes unitários das invariantes já implementadas;
- execução local e imagem de deploy com Docker.

Endpoints de escrita disponíveis nesta primeira fatia:

```text
POST /api/catalog/offers
POST /api/production/items
POST /api/frozen-stock/configurations
POST /api/frozen-stock/production-entries
POST /api/orders/{orderId}/confirmation
```

A entrada de produção e a confirmação de Pedido exigem `Idempotency-Key`. A confirmação também exige `ExpectedVersion` e rejeita alterações concorrentes. O tenant nunca é recebido no payload: ele é obtido da claim autenticada `organization_id`. A configuração de um provedor de identidade e as políticas de autorização ainda serão adicionadas antes de qualquer uso operacional real.

O endpoint de confirmação opera sobre Pedidos abertos e capacidades já persistidos. Os casos de uso públicos para criar o rascunho autoritativo do Pedido e configurar a capacidade diária entram na próxima fatia, antes da integração com os remotes. Créditos de plano e crédito financeiro também serão incorporados à mesma transação quando suas fontes autoritativas forem introduzidas.

Em `Development`, a organização Sabor Santè é selecionada por uma configuração do servidor para manter os fluxos locais utilizáveis enquanto o provedor de identidade não foi escolhido. Esse fallback não funciona fora do ambiente de desenvolvimento; em produção, chamadas a `/api` sem uma identidade autenticada contendo `organization_id` recebem `401`.

## Executar com Docker

```bash
cp .env.example .env
docker compose up --build
```

A API fica em `http://localhost:8080`. Verificações:

```text
GET /health/live
GET /health/ready
GET /openapi/v1.json (ambiente Development)
```

Em desenvolvimento, as migrations pendentes são aplicadas na inicialização. Em produção, devem ser executadas como uma etapa explícita e única do deploy antes de subir novas réplicas.

O PostgreSQL fica acessível apenas em `127.0.0.1:5433` por padrão, configurável por `POSTGRES_PORT`.

## Executar testes

```bash
dotnet restore
dotnet test
dotnet build --configuration Release
```

## Decisões

- PostgreSQL entra desde o início porque confirmação de Pedido e baixa FEFO exigem transação e concorrência reais.
- Redis não foi adicionado: ainda não existe um caso de uso concreto que exija cache ou coordenação distribuída. Quando existir, será incluído no `compose.yaml` e acessado por uma abstração da aplicação.
- O nome e a composição pertencem ao Item Produzível; regras comuns de venda pertencem à Oferta genérica de Congelados; apresentação e preço variável pertencem à `FrozenConfiguration`.
- Dados de negócio implementam o contrato tenant-owned. Filtros globais do EF Core isolam leituras, `SaveChanges` rejeita escritas de outro tenant e chaves estrangeiras compostas impedem referências cruzadas entre organizações.
- Unicidades de negócio e idempotência são locais à organização. A migration multi-tenant associa os dados existentes ao tenant inicial Sabor Santè.
- A confirmação de Pedido usa transação `Serializable`, versão otimista e chave idempotente por Organização; itens congelados são alocados por validade, fabricação e ID estável.
- Impressão será um adapter de infraestrutura separado e nunca alterará lote, estoque ou Pedido.

## Sequência de implementação

1. revisar o scaffold à luz dos fluxos e contratos de interface consolidados — concluído para as fatias implementadas;
2. modelar as fontes autoritativas mínimas de Oferta e Item Produzível — concluído;
3. revisar a migration inicial e estabelecer a fundação multi-tenant — concluído;
4. implementar habilitação de congelado e entrada atômica de lote + `ProductionEntry`, com idempotência — concluído;
5. implementar a primeira fatia transacional de confirmação do Pedido, com capacidade, cobrança e alocação FEFO — concluído;
6. adicionar criação autoritativa do Pedido e configuração da capacidade diária;
7. incorporar créditos de plano, crédito financeiro, composição e restrições à transação de confirmação;
8. integrar o frontend consolidado por meio de adapters, sem transportar interfaces de mock para a API.
