# TS API

API autoritativa da Sabor Santè, construída em .NET 10 e organizada como monólito modular.

> **Estado atual:** o frontend demonstrativo foi consolidado como linha de base em 4 de setembro de 2026 e a implementação autoritativa da API foi retomada. Evoluções posteriores do frontend seguem sem bloquear o backend.

## Estado inicial

Esta primeira fatia estabelece:

- separação entre API, aplicação, domínio e infraestrutura;
- PostgreSQL como persistência transacional;
- domínio inicial de congelados (configuração, lote, validade e movimentação);
- política central de validade de 90 dias corridos usando `DateOnly`;
- fontes autoritativas mínimas de Oferta e Item Produzível;
- criação de Configuração de Congelado com apresentação e preço variável próprios;
- entrada atômica de lote + `EntradaProducao`, protegida por `Idempotency-Key`;
- contrato inicial de confirmação de Pedido;
- health checks de processo e banco;
- testes unitários das invariantes já implementadas;
- execução local e imagem de deploy com Docker.

Endpoints de escrita disponíveis nesta primeira fatia:

```text
POST /api/catalog/offers
POST /api/production/items
POST /api/frozen-stock/configurations
POST /api/frozen-stock/production-entries
```

A entrada de produção exige `Idempotency-Key`. Autenticação, autorização e os demais casos de uso ainda serão adicionados antes de qualquer uso operacional real.

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
- Impressão será um adapter de infraestrutura separado e nunca alterará lote, estoque ou Pedido.

## Sequência de implementação

1. revisar o scaffold à luz dos fluxos e contratos de interface consolidados — em andamento;
2. modelar as fontes autoritativas mínimas de Oferta e Item Produzível;
3. revisar ou substituir a migration inicial;
4. implementar habilitação de congelado e entrada atômica de lote + `ProductionEntry`, com idempotência;
5. implementar a confirmação transacional do Pedido e a alocação FEFO;
6. integrar o frontend já consolidado por meio de adapters, sem transportar interfaces de mock para a API.
