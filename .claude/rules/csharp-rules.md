---
paths: 
  - **/*.cs
---

## C# Conventions

Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and the [.NET Runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md).

### Naming
- `PascalCase` for types, methods, properties, events, constants, and public fields
- `camelCase` for local variables and parameters
- `_camelCase` for private instance fields (prefix with `_`)
- `s_camelCase` for private static fields, `t_camelCase` for private static thread-local fields
- Use meaningful names; avoid abbreviations except for well-known ones (`id`, `url`, `http`)
- Interfaces prefixed with `I` (e.g., `IRouteRepository`)
- Async methods suffixed with `Async` (e.g., `GetRoutesAsync`)

### Code Style
- Use `var` when the type is apparent from the right-hand side; use explicit types otherwise
- Prefer expression-bodied members for single-line methods and properties
- Use file-scoped namespaces (`namespace Foo.Bar;`)
- Use primary constructors where appropriate (.NET 8+)
- Prefer `is null` / `is not null` over `== null` / `!= null`
- Use `string.Empty` instead of `""`
- Place `using` directives outside namespace declarations, grouped (system first, then third-party, then project)

### Design
- Prefer `IServiceCollection` extension methods for registering services (keep `Program.cs` clean)
- Use the Options pattern (`IOptions<T>`) for configuration binding — avoid injecting `IConfiguration` directly into services
- Prefer `record` types for immutable data transfer objects
- Use `CancellationToken` parameters on all async methods that do I/O
- Avoid `async void`; use `async Task` instead

### ASP.NET Core Specifics
- Use minimal APIs (`.MapGroup`, `.MapGet`, etc.) consistent with existing API endpoints
- Apply `[RequireAuthorization]` on endpoint groups rather than individual endpoints where possible
- Return `TypedResults` (e.g., `TypedResults.Ok(...)`, `TypedResults.NotFound()`) instead of `Results`
- Register middleware in the correct order: exception handling → HTTPS → auth → routing → endpoints
