# Clean Architecture Template

A production-ready **.NET 10** Web API template built on **Clean Architecture** principles. It ships with a complete set of enterprise patterns—CQRS, Result pattern, Domain-Driven Design, Outbox pattern, JWT authentication with Redis-backed session management, distributed locking, rate limiting, audit logging, and more—so you can focus on your domain logic from day one.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Installing as a `dotnet new` Template](#installing-as-a-dotnet-new-template)
- [Architecture Overview](#architecture-overview)
- [Layer Details](#layer-details)
  - [Shared Layer](#1-shared-layer)
  - [Domain Layer](#2-domain-layer)
  - [Application Layer](#3-application-layer)
  - [Infrastructure Layer](#4-infrastructure-layer)
  - [WebApi Layer](#5-webapi-layer)
- [Key Patterns & Features](#key-patterns--features)
- [Configuration Reference](#configuration-reference)
- [Docker & Deployment](#docker--deployment)
- [How to Use This Template](#how-to-use-this-template)
- [Tech Stack](#tech-stack)

---

## Quick Start

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Docker & Docker Compose | Latest |

### 1. Start infrastructure services

```bash
docker compose up -d postgres redis seq
```

### 2. Run the API

```bash
cd src/CleanArchitecture.WebApi
dotnet run
```

The API will be available at `https://localhost:5001` (or the port defined in `launchSettings.json`).  
Scalar API docs are served at `/scalar/v1`.

### 3. Run everything in Docker

```bash
docker compose up -d
```

This builds the backend image and starts all services. The API is exposed on port **5005**.

---

## Installing as a `dotnet new` Template

This project is configured as a `dotnet new` template. Install it from the repository root:

```bash
dotnet new install .
```

Then create a new project anywhere:

```bash
dotnet new cleanarchitecture -n MyProject
```

The `sourceName` in the template config is `CleanArchitecture`, so every namespace, project name, and folder will automatically be renamed to `MyProject` (or whatever name you provide).

To uninstall:

```bash
dotnet new uninstall <path-to-repo>
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│                   WebApi Layer                  │
│        (Endpoints, Middleware, Program.cs)       │
├─────────────────────────────────────────────────┤
│               Infrastructure Layer              │
│   (EF Core, Redis, JWT, Serilog, Outbox, etc.)  │
├─────────────────────────────────────────────────┤
│                Application Layer                │
│      (CQRS Handlers, Validators, Abstractions)  │
├─────────────────────────────────────────────────┤
│                  Domain Layer                   │
│       (Entities, Value Objects, Domain Events)  │
├─────────────────────────────────────────────────┤
│                  Shared Layer                   │
│     (Result, Error, Entity, AggregateRoot, etc.)│
└─────────────────────────────────────────────────┘
```

**Dependency flow:** `WebApi → Infrastructure → Application → Domain → Shared`

The inner layers (Domain, Shared) have **zero** dependency on outer layers. The Application layer defines abstractions (interfaces) that the Infrastructure layer implements, following the **Dependency Inversion Principle**.

---

## Layer Details

### 1. Shared Layer

> `CleanArchitecture.Shared` — Framework-level building blocks shared across all layers.

| Component | File | Description |
|-----------|------|-------------|
| **Entity** | `Entity.cs` | Abstract base class for all entities. Provides a `Guid Id` auto-generated on creation. |
| **AggregateRoot** | `AggregateRoot.cs` | Extends `Entity`. Manages a list of `IDomainEvent`s. Exposes `Raise()` to queue events and `ClearEvents()` to flush them after dispatch. |
| **Result / Result\<T>** | `Result.cs` | Discriminated result type. Every handler returns `Result` or `Result<T>` instead of throwing exceptions. Provides `Success()`, `Failure()`, and an implicit operator from `T?`. |
| **Error** | `Error.cs` | Immutable record representing a domain error with an `ErrorCode`, `ErrorType`, and optional `Args` dictionary for parameterized messages. Factory methods: `Validation()`, `BadRequest()`, `NotFound()`, `Unauthorized()`, `Forbidden()`, `Conflict()`, `TooManyRequests()`. |
| **ErrorType** | `ErrorType.cs` | Enum mapping error categories to HTTP status codes (e.g., `BadRequest = 400`, `NotFound = 404`). |
| **ValidationError** | `Error.cs` | Sealed record extending `Error` that carries an array of individual validation errors. |
| **IDomainEvent** | `IDomainEvent.cs` | Marker interface for domain events. |
| **IDomainEventHandler\<T>** | `IDomainEventHandler.cs` | Handler contract for domain events. |
| **IIntegrationEvent** | `IIntegrationEvent.cs` | Marker interface for integration events (cross-boundary). |
| **IIntegrationEventHandler\<T>** | `IIntegrationEventHandler.cs` | Handler contract for integration events. |
| **IAppOption** | `IAppOptions.cs` | Marker interface for strongly-typed configuration option classes. Used by the auto-registration system. |
| **Localization** | `Resources/Languages/` | Resource files (`Lang.en.resx`, `Lang.tr.resx`) for multi-language error messages. Error codes are used as resource keys. |

---

### 2. Domain Layer

> `CleanArchitecture.Domain` — Pure domain model with zero infrastructure dependencies.

#### User Aggregate

The template includes a **User** aggregate root as an example:

| Component | File | Description |
|-----------|------|-------------|
| **User** | `Users/User.cs` | Aggregate root implementing `IAuditable`. Uses a factory method `Create()` that validates all inputs through value objects and raises a `UserRegisteredDomainEvent`. Private constructor enforces creation only through the factory. |
| **Email** | `Users/Email.cs` | Value object (`readonly record struct`). Validates format and max length. |
| **Name** | `Users/Name.cs` | Value object containing `FirstName` and `LastName` with min/max length validation. |
| **Password** | `Users/Password.cs` | Value object that hashes passwords using PBKDF2 with SHA-512 (500K iterations). Provides `VerifyPassword()` with constant-time comparison. |
| **UserErrors** | `Users/UserErrors.cs` | Centralized error definitions organized by sub-domain (`Email`, `Name`, `Password`, `Auth`). Errors include parameterized args (e.g., min length values). |
| **UserRegisteredDomainEvent** | `Users/Events/` | Domain event raised when a new user is created. |

#### Audit System

| Component | File | Description |
|-----------|------|-------------|
| **IAuditable** | `Audit/IAuditable.cs` | Marker interface. Any entity implementing this will be automatically audited on create/update/delete. |
| **AuditLog** | `Audit/AuditLog.cs` | Entity storing audit trail: `UserId`, `EntityName`, `Action`, `OldValues`, `NewValues`, `ChangedColumns`, `IpAddress`, `UserAgent`, `TimestampUtc`. |
| **AuditMaskAttribute** | `Audit/AuditMaskAttribute.cs` | Property attribute that masks sensitive values (e.g., passwords) in audit logs with `***`. |

---

### 3. Application Layer

> `CleanArchitecture.Application` — Use cases, CQRS handlers, validators, and abstractions.

#### CQRS (Command Query Responsibility Segregation)

| Interface | Description |
|-----------|-------------|
| `ICommand` | Marker for commands that return `Result` (no data). |
| `ICommand<TResponse>` | Marker for commands that return `Result<TResponse>`. |
| `ICommandHandler<TCommand>` | Handles commands returning `Result`. |
| `ICommandHandler<TCommand, TResponse>` | Handles commands returning `Result<TResponse>`. |
| `IQuery<TResponse>` | Marker for queries that return `Result<TResponse>`. |
| `IQueryHandler<TQuery, TResponse>` | Handles queries returning `Result<TResponse>`. |

All handlers are **auto-registered** via assembly scanning using Scrutor.

#### Decorators (Cross-Cutting Concerns)

| Decorator | Description |
|-----------|-------------|
| **ValidationDecorator** | Wraps every command handler. Runs all registered `FluentValidation` validators for the command *before* the handler executes. Returns `ValidationError` on failure without ever reaching the handler. |
| **LoggingDecorator** | Wraps every command and query handler. Logs `Processing...` before and `Completed/Failed` after execution with error details on failure. |

Decorator order: **Logging → Validation → Handler** (outermost to innermost).

#### Abstractions (Interfaces)

| Abstraction | Description |
|-------------|-------------|
| `IApplicationDbContext` | EF Core `DbContext` interface exposing `DbSet<User>` and `SaveChangesAsync()`. |
| `ITokenProvider` | Creates JWT access tokens and refresh tokens. |
| `ISessionService` | Redis-backed session management: create, rotate, revoke, blacklist sessions. |
| `IUserContext` | Extracts current user info from `HttpContext` (Id, Jti, IP, UserAgent, remaining token lifetime). |
| `ICacheService` | In-memory cache interface: Get, Set, Remove, Exists, GetOrSet, Clear. |
| `IDistributedCacheService` | Extends `ICacheService` with Redis-specific operations: atomic compare-and-remove, sets, sorted sets, Lua scripting, and batching. |
| `ICacheBatch` | Batch interface for pipelining multiple Redis operations in a single round-trip. |
| `IDomainEventsDispatcher` | Dispatches domain events to their registered handlers. |
| `IEventBus` | Publishes integration events to their handlers. |
| `IDistributedLockManager` | Redis-based distributed locking with optional wait/retry. |
| `IOutboxService` | Stores outbox messages for reliable async processing. |
| `IOutboxSignal` | Signal mechanism for the outbox background processor (notify/wait). |

#### Example Use Cases (User Module)

| Use Case | Type | Description |
|----------|------|-------------|
| **UserRegisterCommand** | Command | Registers a new user, creates JWT tokens, and initializes a session. Raises `UserRegisteredDomainEvent`. |
| **UserLoginCommand** | Command | Authenticates by email/password, creates tokens and session. Enforces max session limit. |
| **UserLogoutCommand** | Command | Revokes the current session and blacklists the access token. |
| **UserLogoutAllCommand** | Command | Revokes all sessions for the current user via Redis Lua script. |
| **UserRefreshTokenCommand** | Command | Rotates access/refresh tokens with a grace period for concurrent requests. |
| **GetUserQuery** | Query | Fetches a single user by ID. |
| **GetUsersQuery** | Query | Fetches all users. |

#### Domain → Integration Event Flow

```
User.Create() 
  → raises UserRegisteredDomainEvent
    → UserRegisteredDomainEventHandler
      → saves UserRegisteredIntegrationEvent to Outbox
        → OutboxBackgroundService processes it
          → InMemoryEventBus publishes to:
            → SendWelcomeEmailHandler
            → SendWelcomeSmsHandler
```

---

### 4. Infrastructure Layer

> `CleanArchitecture.Infrastructure` — Concrete implementations of all application abstractions.

#### Database (PostgreSQL + EF Core)

| Component | Description |
|-----------|-------------|
| **ApplicationDbContext** | EF Core `DbContext` with `Users`, `AuditLogs`, and `OutboxMessages` DbSets. Uses `snake_case` naming convention. Applies configurations from assembly. |
| **DatabaseExtensions** | Registers `NpgsqlDataSource`, configures EF Core with interceptors (`AuditInterceptor`, `DomainEventDispatcherInterceptor`, `OutboxInsertInterceptor`). |
| **DatabaseSchemas** | Static class defining default schema name. |
| **PostgresOptions** | Strongly-typed options (`IAppOption`) with FluentValidation validator. Connection string is built from individual properties. |

#### Authentication (JWT)

| Component | Description |
|-----------|-------------|
| **TokenProvider** | Creates JWT access tokens with `sub` and `jti` claims using HMAC-SHA256. Generates cryptographically random refresh tokens. |
| **SessionService** | Full Redis-backed session lifecycle: login, register, refresh (with grace period), logout, logout-all. Enforces max 5 concurrent sessions. Uses Redis sorted sets for session tracking and Lua scripts for atomic operations. |
| **UserContext** | Extracts user identity from `HttpContext` claims. Lazy-cached properties. |
| **JwtOptions** | Configuration for issuer, audience, secret key, token lifetimes, and grace period. Validated on startup. |
| **AuthSchemes** | Two JWT schemes: `Default` (validates lifetime) and `IgnoreLifetime` (for refresh token endpoint). |
| **Blacklisting** | On logout, the access token's JTI is blacklisted in Redis for its remaining TTL. Every request is checked via `OnTokenValidated` event. |

#### Authorization

| Component | Description |
|-----------|-------------|
| **AuthPolicies** | Defines the `RefreshTokenPolicy` which uses the `IgnoreLifetime` auth scheme (allows expired access tokens for refresh). |

#### Caching (Redis + In-Memory)

| Component | Description |
|-----------|-------------|
| **RedisCacheService** | Full `IDistributedCacheService` implementation using `StackExchange.Redis`. Supports sets, sorted sets, Lua scripting, batching, and instance-namespaced keys. |
| **MemoryCacheService** | `ICacheService` implementation using `IMemoryCache`. Supports bulk clear via `CancellationChangeToken`. |
| **RedisCacheBatch** | Pipelines multiple Redis operations into a single network round-trip. |
| **RedisOptions** | Connection configuration with FluentValidation. |

#### Outbox Pattern

| Component | Description |
|-----------|-------------|
| **OutboxMessage** | Entity stored in the database with `Type`, `Content` (JSON), `OccurredOnUtc`, `ProcessedOnUtc`, `Error`. |
| **OutboxService** | Serializes integration events and adds them as `OutboxMessage` entities to the DbContext. |
| **OutboxInsertInterceptor** | EF Core `SaveChangesInterceptor`. Detects when new `OutboxMessage` entities are added and signals the background processor after successful save. |
| **OutboxBackgroundService** | `BackgroundService` that waits for a signal, then processes unprocessed outbox messages in batches. |
| **OutboxProcessor** | Reads unprocessed messages, deserializes them, and publishes via `IEventBus`. |
| **OutboxSignal** | `SemaphoreSlim`-based signal for efficiently waking the background processor. |

#### Domain Event Dispatching

| Component | Description |
|-----------|-------------|
| **DomainEventDispatcherInterceptor** | EF Core `SaveChangesInterceptor`. Before `SaveChanges`, collects all domain events from aggregate roots, clears them, and dispatches via `IDomainEventsDispatcher`. |
| **DomainEventsDispatcher** | Resolves `IDomainEventHandler<T>` instances from DI and invokes them. Uses `ConcurrentDictionary` for type caching. |

#### Audit Logging

| Component | Description |
|-----------|-------------|
| **AuditInterceptor** | EF Core `SaveChangesInterceptor`. Captures changes for entities implementing `IAuditable`. Records old/new values, changed columns, user ID, IP, and user agent. Respects `[AuditMask]` for sensitive properties. |

#### Distributed Locking

| Component | Description |
|-----------|-------------|
| **RedisLockManager** | `IDistributedLockManager` implementation using Redis `SETNX`-style locks. Supports one-shot and retry-with-timeout patterns. Auto-releases lock in `finally` block. |

#### Rate Limiting

| Component | Description |
|-----------|-------------|
| **RateLimitingExtensions** | Configures global, login, and registration rate limit policies. Uses Redis-backed rate limiting with automatic in-memory fallback when Redis is unavailable. Supports both Fixed Window and Sliding Window algorithms. |
| **RateLimitOptions** | Configurable per-policy settings (`PermitLimit`, `WindowInSeconds`). Validated with FluentValidation. |

#### Structured Logging

| Component | Description |
|-----------|-------------|
| **Serilog** | Configured via `appsettings.json`. Writes to Console and Seq. Enriched with `CorrelationId` from request headers. |

---

### 5. WebApi Layer

> `CleanArchitecture.WebApi` — HTTP entry point, minimal API endpoints, and middleware.

#### Minimal API Endpoints

| Component | Description |
|-----------|-------------|
| **IEndpoint** | Interface with a single `MapEndpoint(IEndpointRouteBuilder)` method. |
| **EndpointExtensions** | Auto-discovers and registers all `IEndpoint` implementations via assembly scanning. |
| **Endpoint Classes** | Each endpoint (e.g., `Register`, `Login`, `Logout`, `RefreshToken`, `Get`, `GetById`) implements `IEndpoint`. Endpoints resolve handlers from DI directly—no MediatR. |

#### Result → HTTP Response Mapping

| Extension | Description |
|-----------|-------------|
| **ToOk()** | Maps `Result` → `200 OK` or ProblemDetails. |
| **ToOk\<T>()** | Maps `Result<T>` → `200 OK` with data or ProblemDetails. |
| **ToNoContent()** | Maps `Result` → `204 No Content` or ProblemDetails. |
| **Localize()** | Resolves error codes to localized messages using `IStringLocalizer<Lang>` with placeholder replacement from `Error.Args`. |

#### API Documentation

| Component | Description |
|-----------|-------------|
| **Scalar** | Interactive API documentation at `/scalar/v1`. Configured with Bearer JWT security scheme. |
| **API Versioning** | Header-based versioning via `X-Version` header using `Asp.Versioning`. |

#### Options Auto-Registration

| Component | Description |
|-----------|-------------|
| **AppOptionExtensions** | Scans specified assemblies for all classes implementing `IAppOption` with a `SectionName` field. Automatically binds them to configuration, wires up FluentValidation, and validates on startup. |

#### Global Exception Handling

| Component | Description |
|-----------|-------------|
| **GlobalExceptionHandler** | Catches unhandled exceptions and returns a standardized `ProblemDetails` response (500). |

#### Middleware

| Component | Description |
|-----------|-------------|
| **RequestContextLoggingMiddleware** | Extracts `Correlation-Id` from request headers (or falls back to `TraceIdentifier`) and pushes it to Serilog's `LogContext` for distributed tracing. |

#### Automatic Migrations

| Component | Description |
|-----------|-------------|
| **MigrationExtensions** | `ApplyMigrations()` runs `Database.Migrate()` on application startup. |

---

## Key Patterns & Features

| Pattern | Description |
|---------|-------------|
| **CQRS** | Strict separation of commands (write) and queries (read). No MediatR—uses direct handler resolution via DI. |
| **Result Pattern** | `Result<T>` replaces exceptions for control flow. Every handler returns a Result. |
| **Domain-Driven Design** | Aggregate roots, value objects, domain events, factory methods, encapsulated business rules. |
| **Outbox Pattern** | Guarantees at-least-once delivery of integration events. Events are stored transactionally in the database, then processed asynchronously by a background service. |
| **Decorator Pattern** | Cross-cutting concerns (validation, logging) applied via the Decorator pattern using Scrutor. |
| **Options Pattern** | Strongly-typed configuration with `IAppOption` marker, automatic binding, and FluentValidation on startup. |
| **Distributed Locking** | Redis-based locks for protecting critical sections in distributed environments. |
| **Audit Logging** | Automatic change tracking for entities marked with `IAuditable`. Sensitive fields can be masked. |
| **Session Management** | Redis-backed JWT sessions with max session limit, token rotation, grace periods, and token blacklisting. |
| **Rate Limiting** | Redis-backed rate limiting with per-endpoint policies and in-memory fallback. |
| **Localization** | Error messages resolved from `.resx` resource files with parameterized placeholders. |

---

## Configuration Reference

All configuration is in `appsettings.json`. Each option section maps to an `IAppOption` class that is validated on startup:

```jsonc
{
  // Structured logging
  "Serilog": {
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "Seq", "Args": { "ServerUrl": "http://seq:5341" } }
    ]
  },
  // PostgreSQL database
  "PostgresOptions": {
    "Username": "postgres",
    "Password": "password",
    "Host": "localhost",
    "Port": 5432,
    "Database": "clean-architecture"
  },
  // Redis cache & session store
  "RedisOptions": {
    "Host": "localhost",
    "Port": 6379,
    "InstanceName": "clean",
    "Database": 0,
    "AbortOnConnectFail": false,
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000,
    "DefaultExpiration": "00:30:00"
  },
  // JWT authentication
  "JwtOptions": {
    "Issuer": "ca-issuer",
    "Audience": "ca-audience",
    "SecretKey": "your-secret-key-min-32-chars-long",
    "AccessTokenLifeTime": "00:30:00",
    "RefreshTokenLifetime": "01:30:00",
    "GracePeriodLifeTime": "00:00:30"
  },
  // Rate limiting
  "RateLimitOptions": {
    "Global": { "PermitLimit": 100, "WindowInSeconds": 60 },
    "Login": { "PermitLimit": 5, "WindowInSeconds": 180 },
    "Registration": { "PermitLimit": 5, "WindowInSeconds": 600 }
  }
}
```

> **Note:** The `.env` file at the root provides environment-variable overrides for Docker Compose (e.g., `PostgresOptions__Username`).

---

## Docker & Deployment

### Development

```bash
docker compose up -d
```

Services: **PostgreSQL 17**, **Redis 7**, **Seq** (log viewer at `http://localhost:8081`), **Backend** (port 5005).

### Production

```bash
docker compose -f docker-compose.prod.yaml up -d
```

Production differences:
- Redis password protection enabled
- Seq admin password required
- No exposed database/cache ports
- Backend joins a `proxy-network` for reverse proxy integration
- Image pulled from GitHub Container Registry

---

## How to Use This Template

### Step 1: Create Your Project

```bash
dotnet new install .                         # From the template repo root
dotnet new cleanarchitecture -n MyProject    # Create a new project
cd MyProject
```

### Step 2: Add a New Entity

1. Create your entity in `Domain/YourModule/`:
   ```csharp
   public class Product : AggregateRoot, IAuditable
   {
       public ProductName Name { get; private set; }
       // ... value objects, private constructor, factory Create() method
   }
   ```

2. Create value objects with validation (follow the `Email`, `Name`, `Password` patterns).

3. Define errors in a `ProductErrors` static class.

### Step 3: Add a Command

1. Create `Application/Products/CreateProduct/CreateProductCommand.cs`:
   ```csharp
   public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<CreateProductResponse>;
   
   internal sealed class CreateProductCommandHandler(
       IApplicationDbContext dbContext) : ICommandHandler<CreateProductCommand, CreateProductResponse>
   {
       public async Task<Result<CreateProductResponse>> Handle(
           CreateProductCommand request, CancellationToken cancellationToken)
       {
           // Your logic here
       }
   }
   ```

2. Add a `CreateProductCommandValidator` using FluentValidation (it will be auto-registered).

### Step 4: Add a Query

```csharp
public sealed record GetProductQuery(Guid Id) : IQuery<ProductResponse>;

internal sealed class GetProductQueryHandler(
    IApplicationDbContext dbContext) : IQueryHandler<GetProductQuery, ProductResponse>
{
    // Your logic here
}
```

### Step 5: Add an Endpoint

Create `WebApi/Endpoints/Products/Create.cs`:

```csharp
internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("products", async (
            [FromBody] CreateProductCommand request,
            ICommandHandler<CreateProductCommand, CreateProductResponse> handler,
            IStringLocalizer<Lang> localizer,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(request, cancellationToken);
            return result.ToOk(localizer);
        })
        .WithTags("Products")
        .RequireAuthorization();
    }
}
```

### Step 6: Register DbSet

Add your entity's `DbSet` to `IApplicationDbContext` and `ApplicationDbContext`:

```csharp
// IApplicationDbContext
DbSet<Product> Products { get; }

// ApplicationDbContext
public DbSet<Product> Products => Set<Product>();
```

### Step 7: Add EF Core Configuration & Migration

1. Create a configuration class in `Infrastructure/Database/Configurations/`.
2. Run: `dotnet ef migrations add AddProduct -p src/CleanArchitecture.Infrastructure -s src/CleanArchitecture.WebApi`

### Step 8: Add Localization

Add error code entries to `Lang.en.resx` and `Lang.tr.resx` (or your supported languages). The error code (e.g., `Product.Name.TooLong`) is used as the resource key.

### Step 9: Add Domain/Integration Events (Optional)

1. Define a domain event record implementing `IDomainEvent`.
2. Raise it in your aggregate's factory/method via `Raise(new YourDomainEvent(...))`.
3. Create a handler implementing `IDomainEventHandler<T>` (auto-registered).
4. For async cross-boundary processing, define an `IIntegrationEvent` and save it via `IOutboxService`.

### Step 10: Add a New Options Section (Optional)

```csharp
public sealed class MyFeatureOptions : IAppOption
{
    public const string SectionName = "MyFeatureOptions";
    public string ApiKey { get; init; } = null!;
}

internal sealed class MyFeatureOptionsValidator : AbstractValidator<MyFeatureOptions>
{
    public MyFeatureOptionsValidator()
    {
        RuleFor(x => x.ApiKey).NotEmpty();
    }
}
```

Add the section to `appsettings.json`—it will be auto-bound and validated on startup.

---

## Tech Stack

| Category | Technology |
|----------|-----------|
| **Runtime** | .NET 10 |
| **Database** | PostgreSQL 17 + Entity Framework Core 10 + Dapper |
| **Cache / Sessions** | Redis 7 (StackExchange.Redis) |
| **Authentication** | JWT Bearer (HMAC-SHA256) |
| **Validation** | FluentValidation |
| **DI Scanning** | Scrutor |
| **Logging** | Serilog → Console + Seq |
| **API Docs** | Scalar + OpenAPI |
| **API Versioning** | Asp.Versioning (header-based) |
| **Rate Limiting** | RedisRateLimiting.AspNetCore + built-in fallback |
| **Naming Convention** | EFCore.NamingConventions (snake_case) |
| **Containerization** | Docker + Docker Compose (dev & prod) |
