# PulseShop

A tiny online store whose heartbeat you can watch — the API behind Dana's **Inside Kestrel** sample.
It's a plain ASP.NET Core app: it knows nothing about Dana, and any client can listen to it.

- **Five services and a database** — gateway, catalog, cart, inventory, payments — in one process
  but calling each other over real HTTP on Kestrel, so every hop is a real request with a real W3C trace.
- **A built-in load generator** plays the customers (browsing, carts, checkouts; 40 req/s by default).
- **It watches itself with what .NET ships:** an `ActivityListener` on ASP.NET Core's and
  `HttpClient`'s own Activities turns every request into a *hop* (who called whom, how long, how it
  ended), plus the GC per generation, heap, allocation rate, thread pool and CPU. No OpenTelemetry
  packages, no NuGet at all.
- **It streams it live:**
  - SignalR hub at `/pulse` — `hops`, `metrics` and `chaos` messages; it also takes commands
    (`SetLoad`, `Latency`, `Fail`, `Starve`, `GcStorm`, `Spike`, `Reset`).
  - Server-sent events at `/live`, with .NET 10's `TypedResults.ServerSentEvents`.
- **A chaos panel at `/`**, made for a phone: add latency, make a service fail, starve the thread
  pool with sync-over-async, storm the GC, spike the traffic.

## Run it

```
cd showcase/PulseShop
dotnet run
```

Then open the **Inside Kestrel** sample in Dana (or any SignalR / SSE client), and
http://localhost:5199 on your phone or browser to break things. To listen on another address:
`dotnet run --urls http://0.0.0.0:5199` (then point the sketch's `HubUrl` at it).

## What to try

| Do this | And watch |
|---|---|
| Payments +400 ms | payment sparks crawl; the gateway's p99 climbs with them |
| Payments fail 30% | red sparks burst at the gateway: one failure cascades up the call chain |
| Starve the thread pool | inventory blocks threads with `Task.Wait()`; the queue piles up at the door and *every* service slows, not just inventory |
| GC storm | allocation rate shoots up; gen 0/1/2 bulbs flash; gen 2 sends a shockwave |
| Spike ×8 | eight times the customers for 8 seconds — together with starvation, a meltdown |
| Back to normal | ...and see how long it really takes to recover |
