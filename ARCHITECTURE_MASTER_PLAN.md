# 🏛️ Master Blueprint 2.0: High-Concurrency Ticketing Engine

## 1. Visão Geral do Domínio e Arquitetura (The Business Problem)
Este é um sistema distribuído de venda de ingressos de altíssima concorrência. O maior desafio técnico é lidar com milhares de requisições simultâneas tentando comprar o mesmo assento no exato mesmo segundo, sem vender ingressos que não existem (Overbooking), garantindo a consistência dos dados através de múltiplos microsserviços.

**Padrões Arquiteturais Aplicados:**
* **Clean Architecture & Domain-Driven Design (DDD):** Organização lógica focada no domínio do negócio.
* **Event-Driven Architecture (EDA):** Comunicação assíncrona entre serviços.
* **CQRS (Command Query Responsibility Segregation):** Separação clara entre leitura e escrita de dados.
* **Database-per-Service:** Autonomia total de dados (nenhum serviço compartilha o mesmo banco).
* **SAGA Pattern (Choreography):** Transações distribuídas com mecanismos de compensação (Rollback) em caso de falhas.

## 2. Stack Tecnológica Enterprise
* **Backend:** C# .NET 10 (Microsserviços isolados).
* **Frontend:** Angular 18+ (SPA - Single Page Application) com controle de estado (NgRx ou Signals).
* **Mensageria:** RabbitMQ orquestrado pelo MassTransit (v8.2.2).
* **Bancos de Dados:** MongoDB (NoSQL para alta performance de leitura/escrita e esquemas flexíveis).
* **API Gateway:** YARP (Yet Another Reverse Proxy) da Microsoft.
* **Infraestrutura e DevOps:** Docker Compose (Local), GitHub Actions (CI/CD).

## 3. Topologia do Sistema
O ecossistema é composto pelas seguintes aplicações:

1. **TicketCatalogService (.NET):** Dono da verdade sobre os shows e capacidade total.
2. **ReservationService (.NET):** Gerencia o inventário em tempo real e concorrência atômica.
3. **PaymentService (.NET):** Processa pagamentos (Stripe/PayPal simulado) e define sucesso/falha do SAGA.
4. **OrderService (.NET):** Acompanha o status do pedido final e emite o ingresso digital.
5. **GatewayService (.NET YARP):** Porta de entrada unificada. Roteia requisições do Frontend, aplica Rate Limiting e CORS.
6. **Ticketing WebApp (Angular):** Interface do usuário final, consome as APIs através do Gateway.

## 4. O Roadmap de Implementação Full-Cycle

### ✅ FASE 1: Fundação e CQRS (Concluído)
- [x] Setup da infraestrutura Docker (MongoDB + RabbitMQ).
- [x] Criação do `TicketCatalogService` e publicação do `ShowCreatedEvent`.
- [x] Criação do `ReservationService` como Consumer assíncrono.
- [x] Eventual Consistency: `ReservationService` cria sua cópia de estoque (`TicketInventory`) no próprio banco de dados.

### 🚀 FASE 2: API Gateway e Frontend Angular
- [ ] Configurar o YARP no `GatewayService` (Roteamento unificado para Catálogo e Reservas).
- [ ] Criar o projeto Angular (Standalone Components).
- [ ] Desenvolver a vitrine de ingressos (Lendo do Catálogo via Gateway).
- [ ] Integrar a chamada de compra (POST para o Reservation via Gateway).

### 🔥 FASE 3: O Padrão SAGA Distribuído (O Coração do Sistema)
- [ ] **Lock Atômico:** Rota de Intenção de Compra no `ReservationService` usando `$inc` e `$gte` no MongoDB para evitar overbooking.
- [ ] **SAGA Step 1:** Disparo do evento `TicketReservedEvent` para a fila.
- [ ] **SAGA Step 2:** O `PaymentService` escuta a reserva, cobra o cartão simulado e dispara `PaymentAcceptedEvent` ou `PaymentRejectedEvent`.
- [ ] **A Compensação (Rollback SAGA):** Se o pagamento falhar, o `ReservationService` ouve o evento de falha e devolve o ingresso para a base (+1 no estoque).

### 🛡️ FASE 4: Resiliência (Nível Staff Engineer)
- [ ] Configurar Dead Letter Queues (DLQ) para mensagens corrompidas.
- [ ] Políticas de Retry no MassTransit (Tentar cobrar o banco 3 vezes antes de falhar).
- [ ] Idempotência: Evitar processamento duplicado caso o RabbitMQ reenvie uma mensagem.

### ⚙️ FASE 5: CI/CD Pipelines (Automação)
- [ ] **Continuous Integration:** GitHub Actions para rodar `dotnet build` e `dotnet test` a cada Push/Pull Request.
- [ ] **Continuous Deployment:** Pipelines para gerar as imagens Docker (`docker build`) e publicar no Docker Hub ou AWS ECR.

### ☁️ FASE 6: Cloud Deployments (Produção)
**Opção A: Enterprise (AWS - Amazon Web Services)**
- *Frontend:* AWS S3 + CloudFront.
- *Backend (APIs):* AWS ECS (Fargate) para rodar os containers Serverless.
- *Mensageria:* Amazon MQ (Managed RabbitMQ).
- *Database:* MongoDB Atlas (AWS Region).

**Opção B: Portfólio Gratuito (Zero Cost)**
- *Frontend:* Vercel ou Netlify (Hospedagem Angular gratuita).
- *Backend (APIs):* Render.com ou Fly.io (Containers Docker gratuitos).
- *Mensageria:* CloudAMQP (Instância RabbitMQ "Little Lemur" grátis).
- *Database:* MongoDB Atlas (Cluster M0 Gratuito de 512MB).