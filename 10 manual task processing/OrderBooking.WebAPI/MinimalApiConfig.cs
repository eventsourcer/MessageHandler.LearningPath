using MessageHandler.EventSourcing.DomainModel;
using OrderAggregate = OrderBooking.OrderBooking;
using OrderBooking.WebAPI.Controllers;
using MessageHandler.EventSourcing.Projections;
using OrderBooking.Projections;
using Azure.Search.Documents;

namespace OrderBooking.WebAPI;

public static class MinimalApiConfig
{
    public static RouteGroupBuilder UseOrderBooking(
        this IEndpointRouteBuilder builder,
        Func<IEndpointRouteBuilder, RouteGroupBuilder> groupBuilder)
    {
        var orderBookings = groupBuilder(builder);

        orderBookings.MapPost("{id}",
        async (IEventSourcedRepository<OrderAggregate> repo, string id, PlacePurchaseOrder cmd) =>
        {
            var booking = await repo.Get(id);
            booking.PlacePurchaseOrder(cmd.PurchaseOrder, cmd.Name);

            await repo.Flush();

            return Results.Created($"api/orderbooking/{id}", booking.Id);
        })
        .Produces(StatusCodes.Status201Created);;

        orderBookings.MapGet("{id}", async(IRestoreProjections<Booking> projector, string id) =>
            Results.Ok(await projector.Restore(nameof(OrderAggregate), id))
        ).Produces(StatusCodes.Status200OK);

        orderBookings.MapGet("pending", async(SearchClient client) =>
        {
            var response = await client.SearchAsync<SalesOrder>("*");
            var pendingOrders = response.Value.GetResults().Select(x => x.Document);

            return TypedResults.Ok(pendingOrders);
        })
        .Produces(StatusCodes.Status200OK);

        orderBookings.MapPost("{bookingId}/confirm",
        async (IEventSourcedRepository<OrderAggregate> repo, string bookingId) =>
        {
            var aggregate = await repo.Get(bookingId);
            aggregate.ConfirmSalesOrder();
            await repo.Flush();

            return Results.Ok(aggregate.Id);
        })
        .Produces(StatusCodes.Status200OK);

        return orderBookings;
    }
}