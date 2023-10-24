using System.Diagnostics;
using EasyNetQ;
using Events;
using Helpers;
using Monolith;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace RandomPlayerService;

public static class Program
{
    private static readonly IPlayer Player = new RandomPlayer();
    
    public static async Task Main()
    {
        var connectionEstablished = false;

        while (!connectionEstablished)
        {
            var bus = ConnectionHelper.GetRMQConnection();
            var subscriptionResult = bus.PubSub.SubscribeAsync<GameStartedEvent>("RPS", e =>
            {
                var propagatorSUB = new TraceContextPropagator();
                var parentContext = propagatorSUB.Extract(default, e, (h, k) =>
                {
                    return new List<string>(new[] { h.Headers.ContainsKey(k) ? h.Headers[k].ToString() : String.Empty });
                });
                Baggage.Current = parentContext.Baggage;
                using var activity = Monitoring.ActivitySource.StartActivity("GameStarted", ActivityKind.Consumer, parentContext.ActivityContext);

                
                var moveEvent = Player.MakeMove(e);
                var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;
                var propagationContext = new PropagationContext(activityContext, Baggage.Current);
                var propagator = new TraceContextPropagator();
                propagator.Inject(propagationContext, moveEvent.Headers, (h, k, v) =>
                {
                    h[k] = v.ToString();
                });

                bus.PubSub.PublishAsync(moveEvent);
            }).AsTask();

            await subscriptionResult.WaitAsync(CancellationToken.None);
            connectionEstablished = subscriptionResult.Status == TaskStatus.RanToCompletion;
            if(!connectionEstablished) Thread.Sleep(1000);
        }

        while (true) Thread.Sleep(5000);
    }
}