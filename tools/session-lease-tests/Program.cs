using System;
using CrazyChat.Overlay;

static class Program
{
    static int Main()
    {
        var fails = 0;
        fails += Expect("same session never yields", () =>
        {
            var local = Lease("a", 100, 100);
            var remote = Lease("a", 100, 120);
            return !OverlaySessionLease.ShouldYield(local, remote, nowUnix: 130, staleAfterSeconds: 45);
        });

        fails += Expect("fresh foreign lease yields", () =>
        {
            var local = Lease("old", 100, 100);
            var remote = Lease("new", 200, 205);
            return OverlaySessionLease.ShouldYield(local, remote, nowUnix: 210, staleAfterSeconds: 45);
        });

        fails += Expect("stale foreign lease does not yield", () =>
        {
            var local = Lease("old", 100, 100);
            var remote = Lease("new", 200, 205);
            return !OverlaySessionLease.ShouldYield(local, remote, nowUnix: 300, staleAfterSeconds: 45);
        });

        fails += Expect("null remote does not yield", () =>
            !OverlaySessionLease.ShouldYield(Lease("a", 1, 1), null, 10, 45));

        fails += Expect("roundtrip json keeps fields", () =>
        {
            var json = OverlaySessionLease.ToJson("sid-1", 11, 22);
            if (!OverlaySessionLease.TryParse(json, out var p) || p == null)
            {
                return false;
            }

            return p.sessionId == "sid-1" && p.startedUnix == 11 && p.heartbeatUnix == 22;
        });

        if (fails > 0)
        {
            Console.Error.WriteLine("FAILED: " + fails + " assertion(s)");
            return 1;
        }

        Console.WriteLine("OK: OverlaySessionLease tests passed");
        return 0;
    }

    static OverlaySessionLease.Payload Lease(string id, long started, long heartbeat)
    {
        return new OverlaySessionLease.Payload
        {
            sessionId = id,
            startedUnix = started,
            heartbeatUnix = heartbeat
        };
    }

    static int Expect(string name, Func<bool> cond)
    {
        if (cond())
        {
            Console.WriteLine("PASS " + name);
            return 0;
        }

        Console.Error.WriteLine("FAIL " + name);
        return 1;
    }
}
