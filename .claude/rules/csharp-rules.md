---
paths:
  - "src/**/*.cs"
---

This document provides guidance for working with code in the Yarp Proxy Manager project.

if you are not sure do not guess, just ask for clarification.
Don't just copy code that follow the same pattern in a difference context.
Don't rely just on names to guess its function, evaluate the code based on the implementation and usage.

## Code Style

- Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and the [.NET Runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md).
- Use the rules defined in the .editorconfig file in the root of the repository for any ambiguous cases
- Write code that is clean, maintainable, and easy to understand
- Favor readability over brevity, but keep methods focused and concise
- **Prefer minimal comments** - The code should be self-explanatory. Add comments sparingly and only to explain *why* a non-intuitive solution was necessary, not *what* the code does. Comments are appropriate for complex logic, public APIs, or domain-specific implementations where context would otherwise be unclear. Use `Check.DebugAssert` instead of a comment if possible.


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
