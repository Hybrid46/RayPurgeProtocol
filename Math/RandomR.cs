using Raylib_cs;
using System;

public static class RandomR
{
    private static System.Random _random = new System.Random();
    private static int _seed = Environment.TickCount;

    /// <summary>
    /// Gets or sets the seed for the random number generator
    /// </summary>
    public static int seed
    {
        get => _seed;
        set
        {
            _seed = value;
            _random = new System.Random(value);
        }
    }

    /// <summary>
    /// Returns a random float between 0.0 [inclusive] and 1.0 [exclusive]
    /// </summary>
    public static float value => (float)_random.NextDouble();

    /// <summary>
    /// Returns a random point inside or on a circle with radius 1
    /// </summary>
    public static Vector2R insideUnitCircle
    {
        get
        {
            float angle = Range(0f, MathF.PI * 2);
            float radius = MathF.Sqrt(Range(0f, 1f));
            return new Vector2R(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }
    }

    /// <summary>
    /// Initialize the random number generator with a seed
    /// </summary>
    public static void InitState(int seed) => RandomR.seed = seed;

    /// <summary>
    /// Returns a random float within [minInclusive..maxInclusive] (range is inclusive)
    /// </summary>
    public static float Range(float minInclusive, float maxInclusive)
    {
        if (minInclusive > maxInclusive)
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);

        float range = maxInclusive - minInclusive;
        return minInclusive + value * range;
    }

    /// <summary>
    /// Returns a random integer within [minInclusive..maxExclusive)
    /// </summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
            (minInclusive, maxExclusive) = (maxExclusive, minInclusive);

        return _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Returns a random boolean value
    /// </summary>
    public static bool boolean => _random.Next(0, 2) == 1;

    /// <summary>
    /// Returns a random color with alpha 1
    /// </summary>
    public static Color color => new Color(value, value, value, 1f);

    /// <summary>
    /// Returns a random color with random alpha
    /// </summary>
    public static Color colorWithAlpha => new Color(value, value, value, value);
}