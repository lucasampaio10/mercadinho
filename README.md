# PDV Mercadinho

> Ponto de venda para balcão com sistema de fiado estilo mercadinho tradicional.

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-12-blue)
![Expo](https://img.shields.io/badge/Expo_SDK-54-black)
![React Native](https://img.shields.io/badge/React_Native-0.81-61DAFB)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791)

---

## ✨ Funcionalidades

- **Cadastro de produtos** — nome, preço, estoque, categoria e código de barras (digitado ou escaneado pela câmera do celular)
- **Venda no balcão** — busca rápida (por nome ou escaneando o código de barras) e finalização (dinheiro, cartão ou fiado)
- **Sistema de fiado** — lança no cliente automaticamente, histórico por cliente, pagamento parcial ou total
- **Dashboard** — resumo do dia: total vendido, nº de vendas, fiado pendente; toque no card "Nº de Vendas" para ver a lista das vendas do dia, e em cada venda para ver o detalhe (cliente, itens com quantidade, observação e horário completo no fuso do celular)

---

## 🏗️ Arquitetura

```
pdv-mercadinho/
├── backend/                    # C# .NET 8 — API REST local
│   ├── PDV.sln
│   └── src/
│       ├── PDV.Domain/         # Entidades, Value Objects, Interfaces, Exceptions
│       ├── PDV.Application/    # Commands, Queries, DTOs, Result Pattern
│       ├── PDV.Infrastructure/ # EF Core, PostgreSQL, Repositórios
│       └── PDV.API/            # Controllers, Middlewares, DI, Program.cs
├── frontend/                   # Expo (React Native) — roda no desktop e mobile
│   ├── app/
│   │   ├── _layout.tsx         # Tab navigation
│   │   ├── index.tsx           # Dashboard
│   │   ├── venda.tsx           # Tela de venda / balcão
│   │   ├── produtos.tsx        # Cadastro de produtos
│   │   └── fiado/
│   │       ├── index.tsx       # Lista de clientes com fiado
│   │       ├── [id].tsx        # Histórico por cliente + pagamento
│   │       └── selecionar.tsx  # Seleção de cliente ao vender no fiado
│   └── src/
│       ├── api/index.ts        # Axios — todos os endpoints
│       ├── store/vendaStore.ts # Zustand — estado global da venda
│       ├── types/index.ts      # Tipos TypeScript
│       └── theme/index.ts      # Cores, espaçamentos, fontes
└── docker-compose.yml          # PostgreSQL local
```

### Padrões do backend

| Camada | Responsabilidade |
|---|---|
| **Domain** | Entidades com regras de negócio, sem dependências externas |
| **Application** | Commands/Queries (CQRS), Result Pattern, DTOs |
| **Infrastructure** | EF Core + PostgreSQL, Repositórios, Unit of Work |
| **API** | Controllers finos, Middleware de exceção, DI |

> 💡 **Por que sem MediatR?** Handlers são injetados diretamente nos controllers via DI, sem o overhead de um mediador. Mantém rastreabilidade direta e reduz dependências para um projeto de escopo único.

---

## 🚀 Como rodar

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Node.js 20.19.4+](https://nodejs.org) — exigido pelo Expo SDK 54
- [Docker](https://www.docker.com) (para o PostgreSQL) — ou PostgreSQL instalado localmente

> Os comandos abaixo estão em duas variantes quando diferem: **PowerShell** (Windows) e **bash** (Linux/macOS).

---

### 1. Subir o banco de dados

```bash
# Na raiz do projeto
docker-compose up -d

# O PostgreSQL ficará disponível em localhost:5432
# Banco: pdv_mercadinho | Usuário: postgres | Senha: postgres
```

---

### 2. Rodar o backend

Os segredos (string de conexão e chave da API) **não são versionados**. Crie o arquivo local a partir do modelo:

```powershell
# PowerShell
cd backend
Copy-Item src/PDV.API/appsettings.Development.example.json src/PDV.API/appsettings.Development.json
```

```bash
# bash
cd backend
cp src/PDV.API/appsettings.Development.example.json src/PDV.API/appsettings.Development.json
```

Edite o arquivo criado e preencha:

| Chave | O que é |
|---|---|
| `POSTGRES_CONNECTION_STRING` | conexão do Postgres (a senha do `docker-compose` é `postgres`) |
| `API_KEY` | chave compartilhada exigida em todo endpoint — gere com `openssl rand -hex 32` |
| `CORS_ORIGINS` | origens que podem chamar a API; acrescente `http://SEU_IP:8081` para usar no celular |

Depois:

```bash
cd backend
dotnet run --project src/PDV.API
```

Qualquer uma das chaves também pode vir por variável de ambiente, que tem precedência:

```powershell
# PowerShell
$env:POSTGRES_CONNECTION_STRING = "Host=localhost;Port=5432;Database=pdv_mercadinho;Username=postgres;Password=postgres"
$env:API_KEY = "sua-chave-aqui"
dotnet run --project src/PDV.API
```

```bash
# bash
POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=pdv_mercadinho;Username=postgres;Password=postgres" \
API_KEY="sua-chave-aqui" \
dotnet run --project src/PDV.API
```

> A API **não sobe sem `API_KEY`** — é proposital. O PDV escuta em `0.0.0.0` para o celular do balcão alcançar, e sem chave qualquer aparelho no Wi-Fi da loja poderia zerar o estoque ou quitar o fiado de terceiros.

As **migrations são aplicadas automaticamente** na inicialização, sob um advisory lock do Postgres — dois processos subindo juntos não disputam a tabela de migrations.

A API ficará disponível em: **http://localhost:5000**
Swagger: **http://localhost:5000/swagger** (use o botão *Authorize* para informar a chave)
Health check: **http://localhost:5000/health** (único endpoint anônimo)

---

### 3. Rodar o frontend

```powershell
# PowerShell
cd frontend
Copy-Item .env.example .env
npm install
npx expo start
```

```bash
# bash
cd frontend
cp .env.example .env
npm install
npx expo start
```

No `.env`, preencha `EXPO_PUBLIC_API_KEY` com **a mesma chave** do `API_KEY` do backend — sem ela toda chamada volta 401.

> ⚠️ Rode `npm install` **dentro de `frontend/`**. A raiz do repositório não tem `package.json` de propósito: instalar ali cria um segundo `react-dom` e o bundle quebra.

Opções no terminal do Expo:
- **`w`** → abre no navegador (desktop, recomendado para PDV)
- **`a`** → abre no Android
- **`i`** → abre no iOS

**Se o bundle vier com erro estranho após trocar de branch ou mexer em dependências**, limpe o cache do Metro:

```bash
npx expo start --clear
```

> ⚠️ **Este app é feito para mobile.** A opção `w` (web) existe só de apoio e quebra: o Expo Router faz *server-side rendering* em Node.js, e o `AsyncStorage` usado pelo `persist` do `vendaStore` (veja [Decisões de arquitetura](#-decisões-de-arquitetura)) tenta acessar `window` nesse ambiente, que não existe no SSR — o processo cai com `ReferenceError: window is not defined`. Isso **não afeta Android/iOS** (opções `a`/`i` ou Expo Go): sem SSR, o bundle sobe limpo. Use `w` só sabendo disso; para desktop instalável sem esse problema, veja a seção abaixo.

#### Conferindo as dependências

O projeto está no **Expo SDK 54**, que fixa as versões de `react`, `react-native` e dos módulos nativos. Se você adicionar um pacote e algo quebrar, valide o alinhamento:

```bash
npx expo install --check   # só reporta o que está fora do esperado
npx expo install --fix     # corrige as versões para as do SDK
```

> ⚠️ Instale pacotes do ecossistema Expo/React Native com **`npx expo install <pacote>`**, não com `npm install <pacote>` — o `expo install` escolhe a versão compatível com o SDK. E não rode `npm audit fix --force`: ele ignora as versões fixadas pelo SDK e quebra o build.

> ⚠️ **`npm audit` reporta várias vulnerabilidades "high"/"critical" (`nanoid`, `postcss`, `shell-quote`, `undici`, `ws`, etc).** São todas em dependências transitivas do **toolchain de dev** do Expo/Metro (CLI, bundler, dev server) — nada disso entra no bundle que roda no celular. É esperado em qualquer projeto Expo SDK 54 e não é motivo pra rodar `npm audit fix --force` (veja o aviso acima).

> 💡 **Desktop instalável:** gere o build estático e sirva com qualquer servidor estático:
> ```bash
> npx expo export --platform web
> npx serve dist
> ```

---

### 4. Rodar em mobile (celular físico)

**1.** No `frontend/.env`, troque `localhost` pelo IP da sua máquina na rede local:

```
EXPO_PUBLIC_API_URL=http://192.168.X.X:5000
```

O bind já é em todas as interfaces (`http://0.0.0.0:5000`, definido no `Kestrel` do `appsettings.json`) — não é preciso setar `ASPNETCORE_URLS`.

**2.** Se for abrir no navegador do celular (Expo web), acrescente a origem ao `CORS_ORIGINS` do `appsettings.Development.json`:

```json
"CORS_ORIGINS": ["http://localhost:8081", "http://192.168.X.X:8081"]
```

O app nativo (Android/iOS) não passa por CORS — só precisa da `EXPO_PUBLIC_API_KEY` correta.

**3.** Reinicie o Expo com `npx expo start --clear` — variáveis `EXPO_PUBLIC_*` são embutidas no bundle, então mudanças no `.env` só valem após um restart com cache limpo.

---

## 📋 Endpoints da API

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/vendas/dashboard` | Resumo do dia |
| `GET` | `/api/vendas/hoje` | Vendas finalizadas hoje, com itens — mais recentes primeiro |
| `GET` | `/api/produtos` | Listar produtos ativos |
| `GET` | `/api/produtos/buscar?nome=X` | Buscar por nome |
| `GET` | `/api/produtos/codigo-barras/{codigo}` | Buscar produto pelo código de barras exato (leitura da câmera) — `404` se não achar |
| `POST` | `/api/produtos` | Criar produto |
| `PUT` | `/api/produtos/{id}` | Atualizar produto |
| `DELETE` | `/api/produtos/{id}` | Inativar produto |
| `POST` | `/api/vendas` | Iniciar venda |
| `GET` | `/api/vendas/{id}` | Estado da venda — restaura o carrinho após refresh |
| `POST` | `/api/vendas/{id}/itens` | Adicionar item — retorna a venda atualizada |
| `PUT` | `/api/vendas/{id}/itens/{produtoId}` | Definir a quantidade do item — retorna a venda atualizada |
| `DELETE` | `/api/vendas/{id}/itens/{produtoId}` | Remover item — retorna a venda atualizada |
| `POST` | `/api/vendas/{id}/finalizar` | Finalizar venda |
| `GET` | `/api/clientes` | Listar clientes com fiado |
| `POST` | `/api/clientes` | Criar cliente |
| `GET` | `/api/clientes/{id}/fiado` | Histórico de fiado |
| `POST` | `/api/clientes/{id}/pagamento` | Registrar pagamento |

Todos exigem o header `X-API-Key`; só `/health` é anônimo.

---

## 📐 Decisões de arquitetura

**Result Pattern em vez de exceptions**
Fluxos de negócio (produto não encontrado, estoque insuficiente) retornam `Result<T>` em vez de lançar exceptions. Isso torna o código previsível e testável.

**Soft delete em Produtos**
Produtos são inativados (`Ativo = false`), nunca deletados fisicamente. Isso preserva o histórico de vendas.

**Snapshot de preço no ItemVenda**
O preço e nome do produto são copiados para o `ItemVenda` no momento da venda. Se o preço mudar depois, o histórico permanece correto.

**FIFO no pagamento de fiado**
Ao registrar um pagamento, os itens de fiado mais antigos são marcados como pagos primeiro (First In, First Out).

**O servidor é a fonte de verdade do carrinho**
Adicionar e remover itens retorna a `Venda` inteira, e o front substitui o carrinho por ela. O total exibido é o `decimal` calculado pelo backend — nunca uma soma local em `float`. Assim o valor na tela é sempre o mesmo que será cobrado ou lançado no fiado.

**Venda em andamento sobrevive ao refresh**
O `vendaId` é persistido em `AsyncStorage`. Ao voltar para a tela de venda, o app faz `GET /api/vendas/{id}` e reconcilia; se a venda não existe mais ou já foi fechada, o estado local é descartado.

**"Hoje" é o dia do mercadinho, não o dia UTC**
O dashboard recorta o dia pelo fuso local (`FusoLocal`), convertendo para UTC apenas na query. Sem isso, no Brasil o resumo do dia zeraria às 21h.

**Vendido, recebido e fiado são números diferentes**
O dashboard mostra os três: `TotalVendidoHoje` é o giro (inclui o fiado), `TotalRecebidoHoje` é o que virou caixa e `TotalFiadoHoje` é o que saiu fiado. Um total único faria a venda fiada aparecer somada ao "vendido" e ao "fiado pendente" ao mesmo tempo, e o fechamento não bateria com o dinheiro na gaveta.

**Horário sempre em UTC até a última hora**
`Entity.CreatedAt`/`UpdatedAt` nascem com `DateTime.UtcNow`, mas o Npgsql por padrão devolve `Kind=Unspecified` ao reler do banco — mesmo a coluna sendo `timestamp with time zone`. Sem tratar isso, o JSON sai sem o "Z" e o app interpretaria o horário como se já fosse do fuso do celular, deslocando a hora exibida pela diferença para UTC. Um conversor global em `PdvDbContext.ConfigureConventions` (`PdvDbContext.cs`) força `Kind=Utc` na volta do banco para todo `DateTime`/`DateTime?` da aplicação — é o que garante que o horário mostrado na tela de Vendas acompanha o fuso do aparelho do operador, não o do servidor.

**Observação da venda e nome do cliente na listagem**
`Venda.Observacao` é uma nota opcional (até 500 caracteres) digitada pelo operador antes de finalizar — vale para as três formas de pagamento, já que o campo fica na tela de venda antes da escolha. `VendaResponse.ClienteNome` só vem preenchido quando o cliente já foi carregado por quem monta a resposta (finalização no fiado, listagem do dia); os demais endpoints deixam `null` em vez de sempre fazer um `JOIN` que a maioria das chamadas não precisa.

**Código de barras opcional, lido pela câmera**
`CodigoBarras` é um campo opcional em `Produto`, lido pelo `expo-camera` em dois pontos: no cadastro/edição do produto (preenche o campo) e na venda (busca exata via `GET /api/produtos/codigo-barras/{codigo}` e adiciona direto ao carrinho, valendo para as três formas de pagamento — a forma só é escolhida depois, ao finalizar). Sem índice único: dois produtos podem ficar com o mesmo código sem que a API rejeite, a busca por código simplesmente devolve o primeiro ativo que encontrar.

**Chave de API única em vez de login**
Não há usuários no balcão, mas também não há fronteira de segurança na rede local. Uma chave compartilhada no header `X-API-Key` elimina o acesso anônimo sem introduzir cadastro de operador; se um dia houver mais de um caixa, o `ApiKeyAuthenticationHandler` é o ponto de troca.

**Validação em duas camadas**
Os `Command`/`Request` carregam DataAnnotations (`[Range]`, `[StringLength]`) para barrar lixo antes do handler; as invariantes de negócio continuam no domínio, que é a última palavra. O `InvalidModelStateResponseFactory` devolve o mesmo formato `{ error }` dos erros de negócio, para o front ter um caminho só de tratamento.

---

## 🧪 Testes

```bash
cd backend
dotnet test
```

58 testes em `backend/tests/PDV.Tests`, sem banco e sem rede — os repositórios são fakes em memória (`Fakes/RepositoriosEmMemoria.cs`), então a suíte roda em menos de um segundo.

| Arquivo | Cobre |
|---|---|
| `Domain/VendaTests.cs` | carrinho, alteração de quantidade, teto por item, regras de finalização |
| `Domain/ProdutoClienteTests.cs` | preço/estoque, débito e crédito, FIFO com abatimento parcial do fiado, igualdade de entidades |
| `Application/VendaHandlersTests.cs` | orquestração dos handlers de venda, forma de pagamento inválida, débito de estoque no fechamento |
| `Application/DashboardQueryTests.cs` | separação vendido/recebido/fiado, recorte do dia local, pagamento de fiado |

---

## ☁️ Produção (Render + Neon)

O backend roda em produção como container Docker no **Render** (`backend/Dockerfile`), com o Postgres hospedado no **Neon** (serverless, plano gratuito).

| Serviço | O quê | URL |
|---|---|---|
| API | Web Service Docker no Render | https://pdv-mercadinho-api.onrender.com |
| Banco | Projeto Postgres 16 no Neon | painel: [console.neon.tech](https://console.neon.tech) |

**Variáveis de ambiente** (configuradas direto no dashboard do Render, nunca commitadas):
- `API_KEY` — chave de produção, diferente da usada em dev local
- `POSTGRES_CONNECTION_STRING` — aponta para o host **não-pooled** do Neon (`ep-...aws.neon.tech`, sem `-pooler`). O app usa um *advisory lock* do Postgres para aplicar migrations com segurança contra deploys concorrentes (veja `ApplyMigrationsAsync`), e isso exige uma conexão de sessão — o pooler do Neon roda em modo *transaction*, que não sustenta advisory lock entre statements.
- `CORS_ORIGINS` — mesma lista de sempre; não afeta o app nativo (Android/iOS), só importa se algum dia abrir a API pelo navegador

**Porta dinâmica:** Render atribui a porta via `$PORT`, diferente do `5000` fixo usado em dev. O `Kestrel:Endpoints` do `appsettings.json` tem prioridade sobre `WebHost.UseUrls()` — por isso `Program.cs` sobrescreve a própria chave de configuração (`builder.Configuration["Kestrel:Endpoints:Http:Url"]`) quando `$PORT` existe, em vez de usar `UseUrls()`, que seria silenciosamente ignorado.

**Plano gratuito do Render "dorme"** depois de ~15 min sem tráfego — a primeira requisição depois disso demora uns 30-50s para acordar o container. Normal, não é bug.

O app mobile aponta para essa URL via `frontend/.env` (`EXPO_PUBLIC_API_URL`) — funciona de qualquer rede, não só da loja.

---

## 📦 Stack

**Backend:** .NET 8, C# 12, ASP.NET Core, Entity Framework Core, Npgsql, PostgreSQL, xUnit  
**Frontend:** Expo SDK 54, React Native 0.81, React 19, Expo Router 6, Zustand, Axios  
**Infra:** Docker, PostgreSQL 16
