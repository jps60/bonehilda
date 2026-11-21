namespace Bonehilda;

internal static class IdleDeck
{
    internal const int Dust = 1, Trophy = 2, Sit = 4;

    internal static int Choose(int available, ref int remaining, int previous, float roll)
    {
        var choices = available & remaining;
        if (choices == 0) { remaining = available; choices = available; }
        if ((choices & ~previous) != 0) choices &= ~previous;
        var count = 0;
        for (var bit = 1; bit <= Sit; bit <<= 1) if ((choices & bit) != 0) count++;
        if (count == 0) return 0;
        var index = System.Math.Min(count - 1, System.Math.Max(0, (int)(roll * count)));
        for (var bit = 1; bit <= Sit; bit <<= 1)
            if ((choices & bit) != 0 && index-- == 0) { remaining &= ~bit; return bit; }
        return 0;
    }

    internal static bool ContinueSitting(float roll, float chance) => roll < chance;
}
