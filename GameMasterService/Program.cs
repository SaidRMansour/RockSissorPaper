using System.Diagnostics;
using EasyNetQ;
using Events;
using Helpers;
using Monolith;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

public class Program
{
    private static Game game = new Game();

    public static async Task Main()
    {
        var connectionEstablished = false;

        using var bus = ConnectionHelper.GetRMQConnection();
        while (!connectionEstablished)
        {
            var subscriptionResult = bus.PubSub
                .SubscribeAsync<PlayerMovedEvent>("RPS", e =>
                {
                    var propagator = new TraceContextPropagator();
                    var parentContext = propagator.Extract(default, e, (h, k) =>
                    {
                        return new List<string>(new[] { h.Headers.ContainsKey(k) ? h.Headers[k].ToString() : String.Empty });
                    });
                    Baggage.Current = parentContext.Baggage;
                    using var activity = Monitoring.ActivitySource.StartActivity("PlayerMoved", ActivityKind.Consumer, parentContext.ActivityContext);

                    var finishedEvent = game.ReceivePlayerEvent(e);
                    if (finishedEvent != null)
                    {
                        var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;
                        var propagationContext = new PropagationContext(activityContext, Baggage.Current);
                        var propagatorPUBFinishedEvent = new TraceContextPropagator();
                        propagatorPUBFinishedEvent.Inject(propagationContext, finishedEvent.Headers, (h, k, v) =>
                        {
                            h[k] = v;
                        });
                        bus.PubSub.PublishAsync(finishedEvent);
                    }
                })
                .AsTask();

            await subscriptionResult.WaitAsync(CancellationToken.None);
            connectionEstablished = subscriptionResult.Status == TaskStatus.RanToCompletion;
            if (!connectionEstablished) Thread.Sleep(1000);
        }
        var gameStartEvent = game.Start();

        using var activity = Monitoring.ActivitySource.StartActivity();
        var propagationContext = new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current);
        var propagator = new TraceContextPropagator();
        propagator.Inject(propagationContext, gameStartEvent.Headers, (h, k, v) =>
        {
            h[k] = v;
        });
        await bus.PubSub.PublishAsync(gameStartEvent);

        while (true) Thread.Sleep(5000);
    }
}