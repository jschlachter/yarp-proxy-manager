namespace West94.ProxyManager.Endpoints;

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/routes").WithTags("Routes");

        group.MapGet("/", () => TypedResults.Ok("This endpoint will return a list of routes.")).WithName("GetRoutes");
        group.MapPost("/", () => TypedResults.Ok("This endpoint will create a new route.")).WithName("CreateRoute");
        group.MapGet("/{id}", (string id) => TypedResults.Ok($"This endpoint will return details for route with ID: {id}")).WithName("GetRouteById");
        group.MapPut("/{id}", (string id) => TypedResults.Ok($"This endpoint will update the route with ID: {id}")).WithName("UpdateRoute");
        group.MapDelete("/{id}", (string id) => TypedResults.Ok($"This endpoint will delete the route with ID: {id}")).WithName("DeleteRoute");
        
        return app;
    }
    
}