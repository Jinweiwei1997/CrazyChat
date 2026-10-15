using UnityEngine;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>
    /// One local sea for every fishing hull on this machine.
    /// The same swell travels left to right: screen X delays the sample.
    /// Not sent to other clients, so two players do not share the same timing.
    /// </summary>
    public static class OverlayFishingBob
    {
        const float CycleSeconds = 7.4f;
        const float Amplitude = 1f / 3f;
        const float CrossScreenSeconds = 4.6f;
        // One small rise-and-fall. Slower than the earlier 1.1s chop.
        const float SmallHalfPeriod = 2f;

        public struct Pose
        {
            public Vector2 Offset;
            public float RollDegrees;
        }

        public static Pose SampleAt(float time, float screenX, float screenWidth)
        {
            var width = Mathf.Max(1f, screenWidth);
            var across = Mathf.Clamp01(screenX / width);
            return Sample(time - across * CrossScreenSeconds);
        }

        public static Pose Sample(float time)
        {
            if (time < 0f) time = 0f;
            var cycle = Mathf.FloorToInt(time / CycleSeconds);
            var t = time - cycle * CycleSeconds;
            float bigStart;
            float bigDur;
            float heave;
            float roll;
            float sway;
            switch ((cycle / 3) % 3)
            {
                case 0:
                    bigStart = 2.35f;
                    bigDur = 2.15f;
                    heave = 15f;
                    roll = 6.2f;
                    sway = 2.8f;
                    break;
                case 1:
                    bigStart = 2.55f;
                    bigDur = 2.05f;
                    heave = 14.2f;
                    roll = 5.8f;
                    sway = -2.6f;
                    break;
                default:
                    bigStart = 2.20f;
                    bigDur = 2.25f;
                    heave = 16f;
                    roll = 6.8f;
                    sway = 3.0f;
                    break;
            }

            var pose = SmallPose(time);
            // One big swell every third cycle, shared by every hull on this machine.
            if (cycle % 3 == 0)
            {
                var mask = SmallMask(t, bigStart, bigDur);
                pose.Offset *= mask;
                pose.RollDegrees *= mask;
                AddWave(ref pose, t, bigStart, bigDur, heave, roll, sway);
            }

            pose.Offset *= Amplitude;
            pose.RollDegrees *= Amplitude;
            return pose;
        }

        static Pose SmallPose(float time)
        {
            var w = Mathf.PI / SmallHalfPeriod;
            var slow = time * (w / 3.1f) + 0.4f;
            // Slightly uneven period and height so neighboring chops are not copies.
            var a = time * w + 0.1f * Mathf.Sin(time * 0.47f);
            var height = 0.86f + 0.14f * Mathf.Sin(time * 0.31f);
            var tilt = 0.84f + 0.16f * Mathf.Sin(time * 0.53f + 1.1f);
            return new Pose
            {
                Offset = new Vector2(
                    Mathf.Sin(slow) * 1.8f * tilt,
                    (Mathf.Sin(a) * 6.6f + Mathf.Sin(slow + 0.6f) * 1.7f) * height),
                RollDegrees = (Mathf.Cos(a) * 2.55f + Mathf.Cos(slow) * 0.7f) * tilt
            };
        }

        static float SmallMask(float t, float start, float duration)
        {
            if (duration <= 0f) return 1f;
            var u = (t - start) / duration;
            if (u <= 0f || u >= 1f) return 1f;
            var duck = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f);
            return 1f - duck;
        }

        static void AddWave(ref Pose pose, float time, float start, float duration, float heave, float roll, float sway)
        {
            if (duration <= 0f) return;
            var u = (time - start) / duration;
            if (u <= 0f || u >= 1f) return;
            const float rise = 0.58f;
            var lift = Heave(u, rise);
            pose.Offset.x += lift * sway;
            pose.Offset.y += lift * heave;
            pose.RollDegrees += Pitch(u, rise) * roll;
        }

        static float Heave(float u, float rise)
        {
            if (u < rise) return Mathf.Sin(u / rise * Mathf.PI * 0.5f);
            var down = (u - rise) / (1f - rise);
            return Mathf.Cos(down * Mathf.PI * 0.5f);
        }

        static float Pitch(float u, float rise)
        {
            if (u < rise) return Mathf.Sin(u / rise * Mathf.PI);
            var down = (u - rise) / (1f - rise);
            return -Mathf.Sin(down * Mathf.PI);
        }
    }
}
