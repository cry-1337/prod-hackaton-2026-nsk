# Backend

These conventions apply to all four projects in `Backend.slnx`.

## Projects

| Project | Responsibilities |
| --- | --- |
| `Backend.Api` | ASP.NET Core 10 controllers and HTTP contracts, Mapster 10 mapping, Serilog 4.x logging and configurable Scalar UI for OpenAPI |
| `Backend.Application` | Source-generated Mediator 3.x commands and queries, FluentValidation 12 validators, handlers, DTOs and common interfaces |
| `Backend.Domain` | Entities, value objects and domain rules |
| `Backend.Infrastructure` | EF Core 10 and Npgsql 10 persistence, PostgreSQL 17 migrations and Redis 7 distributed caching |

Use the .NET 10 SDK. Package versions live in `Directory.Packages.props`. Keep dependencies directed toward Application and Domain. Api composes the application and infrastructure services.

## Code Style

### Formatting

- Use K&R braces. Put the opening brace on the same line and write `} else {` and `} catch (...) {`.
- Use one file-scoped namespace per C# file that declares types. Use top-level statements for the API entry point in `Program.cs`.
- Indent with four spaces and no tabs. Use a single space around an assignment operator and omit alignment padding.
- Separate validation rules and logical steps with blank lines. Wrap long parameter lists and initializers onto multiple lines. Keep the code self-explanatory and omit comments.
- Follow the repository's `.editorconfig`.

### Naming

| Element | Convention | Example |
| --- | --- | --- |
| Classes, methods, properties | PascalCase | `RegisterUserHandler` |
| Local variables, parameters | camelCase | `userId` |
| Private instance fields | `m_` followed by PascalCase | `m_Settings` |
| Private static fields | `m_` followed by PascalCase | `m_DefaultRole` |
| Enum types | `E` prefix | `EOrderStatus` |
| Enum members | PascalCase | `EUserRole.Admin` |
| Interfaces | `I` prefix | `IOrderRepository` |
| Entities | `Entity` suffix | `OrderEntity` |
| DTOs | `Dto` suffix | `OrderDto` |
| Commands | `Command` suffix | `CreateOrderCommand` |
| Queries | `Query` suffix | `GetOrderQuery` |
| Results | `Result` suffix | `CreateOrderResult` |
| Handlers | `Handler` suffix | `CreateOrderHandler` |
| Consumers | `Consumer` suffix | `UserRegisteredConsumer` |
| API requests | `Request` suffix | `CreateOrderRequest` |
| API responses | `Response` suffix | `CreateOrderResponse` |

### Language Features

- Use primary constructors for services, handlers, controllers, consumers and DbContexts with dependencies.
- Use `var` for locals. Write explicit types in signatures and field declarations.
- Initialize non-nullable entity strings with `= null!;`. Use `string?` for optional strings. Omit the `required` modifier on entities.
- Use `?? throw` when a missing value must raise an exception.
- Seal records, pipeline behaviors and internal implementations.
- Put `CancellationToken cancellationToken` last in every async method. Pass it to Mediator, validators, persistence and cache operations.

### Access Modifiers

Write accessibility explicitly. Declare API-facing types and members `public`, concrete infrastructure services `internal sealed`, and private fields and helpers `private`.

### CQRS File Layout

Put commands in `Backend.Application/Commands/<Feature>` and queries in `Backend.Application/Queries/<Feature>`. Keep each message with its handler and any validator or operation-specific result in a file named for the operation. Use source-generated Mediator with a scoped lifetime and run FluentValidation before handlers.

Put each application interface in its own same-named file under `Backend.Application/Common/Interfaces`. Handlers use EF Core through `IAppDbContext`. Put each shared DTO in its own file under `Backend.Application/Dto`.

### EF Core Entities

Configure entities and their properties with data annotations. Put class-level annotations such as `[Table]` and `[Index]` on the entity and property annotations such as `[Key]` and `[Column]` on their properties. Keep mappings on the entity instead of duplicating them in the fluent API. Use expression-bodied DbSet properties.

```csharp
public DbSet<OrderEntity> Orders => Set<OrderEntity>();
```

### Controllers

Use `[ApiController]`, attribute routing and primary constructor injection of `IMediator`. Keep HTTP requests in `Backend.Api/Contracts/Requests` and HTTP responses in `Backend.Api/Contracts/Responses`, one type per file. Map requests to application messages and application DTOs to responses with Mapster in Api. Pass the request's cancellation token to `Send`. On creation, return `201 Created` with a location pointing to the GET action.

### Error Message Strings

Use `SCREAMING_SNAKE_CASE` string literals for application error codes, such as `ORDER_NOT_FOUND`, `EMAIL_ALREADY_EXISTS` and `INVALID_CREDENTIALS`. Use FluentValidation's `WithErrorCode` for individual rules. Return validation errors as Problem Details.

```csharp
var order = await orders.FindAsync(query.OrderId, cancellationToken)
    ?? throw new NotFoundException("ORDER_NOT_FOUND");
```
