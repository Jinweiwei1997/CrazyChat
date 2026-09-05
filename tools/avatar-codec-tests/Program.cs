using System;
using System.Collections.Generic;
using System.IO;
using CrazyChat.Overlay;
using CrazyChat.Overlay.Interact;

static class Program
{
    static int Main()
    {
        var fails = 0;
        var dir = Path.Combine(Path.GetTempPath(), "crazychat-avatar-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "a.png");
        var b = Path.Combine(dir, "b.png");

        fails += Expect("disabled when version 0", () => !OverlayAvatarRules.IsEnabled(0, a, b));
        File.WriteAllBytes(a, new byte[] { 1, 2, 3 });
        fails += Expect("disabled when only A", () => !OverlayAvatarRules.IsEnabled(1, a, b));
        File.WriteAllBytes(b, new byte[] { 4, 5 });
        fails += Expect("enabled when version and both files", () => OverlayAvatarRules.IsEnabled(3, a, b));

        fails += Expect("presence encode/decode", () =>
        {
            var on = OverlayAvatarSync.EncodePresence(true);
            var off = OverlayAvatarSync.EncodePresence(false);
            bool a1, a0;
            return OverlayAvatarSync.TryDecodePresence(on, out a1) && a1
                   && OverlayAvatarSync.TryDecodePresence(off, out a0) && !a0;
        });

        fails += Expect("chunk roundtrip", () =>
        {
            var src = new byte[5000];
            for (var i = 0; i < src.Length; i++)
            {
                src[i] = (byte)(i % 251);
            }

            var msgs = OverlayAvatarSync.EncodeChunks('B', 9, src);
            if (msgs.Count < 1)
            {
                return false;
            }

            var map = new Dictionary<int, string>();
            var total = 0;
            for (var i = 0; i < msgs.Count; i++)
            {
                char slot;
                int ver, idx, tot;
                string piece;
                if (!OverlayAvatarSync.TryDecodeChunk(msgs[i], out slot, out ver, out idx, out tot, out piece))
                {
                    return false;
                }

                if (slot != 'B' || ver != 9)
                {
                    return false;
                }

                total = tot;
                map[idx] = piece;
            }

            byte[] png;
            if (!OverlayAvatarSync.TryAssemble(map, total, out png) || png.Length != src.Length)
            {
                return false;
            }

            for (var i = 0; i < src.Length; i++)
            {
                if (png[i] != src[i])
                {
                    return false;
                }
            }

            return true;
        });

        try
        {
            Directory.Delete(dir, true);
        }
        catch
        {
        }

        if (fails > 0)
        {
            Console.Error.WriteLine("FAILED: " + fails);
            return 1;
        }

        Console.WriteLine("OK: avatar codec/sync tests passed");
        return 0;
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
