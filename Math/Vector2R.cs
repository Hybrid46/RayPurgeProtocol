using System.Globalization;
using System.Runtime.CompilerServices;

public struct Vector2R : IEquatable<Vector2R>, IFormattable
{
    public float x;
    public float y;

    private static readonly Vector2R zeroVector = new Vector2R(0f, 0f);
    private static readonly Vector2R oneVector = new Vector2R(1f, 1f);
    private static readonly Vector2R upVector = new Vector2R(0f, 1f);
    private static readonly Vector2R downVector = new Vector2R(0f, -1f);
    private static readonly Vector2R leftVector = new Vector2R(-1f, 0f);
    private static readonly Vector2R rightVector = new Vector2R(1f, 0f);

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return index switch
            {
                0 => x,
                1 => y,
                _ => throw new IndexOutOfRangeException("Invalid Vector2 index!")
            };
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (index)
            {
                case 0:
                    x = value;
                    break;
                case 1:
                    y = value;
                    break;
                default:
                    throw new IndexOutOfRangeException("Invalid Vector2 index!");
            }
        }
    }

    public Vector2R normalized
    {
        get
        {
            float mag = magnitude;
            return mag > float.Epsilon ? this / mag : zero;
        }
    }

    public float magnitude => (float)Math.Sqrt(x * x + y * y);
    public float sqrMagnitude => x * x + y * y;

    public static Vector2R zero => zeroVector;
    public static Vector2R one => oneVector;
    public static Vector2R up => upVector;
    public static Vector2R down => downVector;
    public static Vector2R left => leftVector;
    public static Vector2R right => rightVector;

    public Vector2R(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(float newX, float newY)
    {
        x = newX;
        y = newY;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R Scale(Vector2R a, Vector2R b)
    {
        return new Vector2R(a.x * b.x, a.y * b.y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Scale(Vector2R scale)
    {
        x *= scale.x;
        y *= scale.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        float mag = magnitude;
        if (mag > float.Epsilon)
        {
            x /= mag;
            y /= mag;
        }
        else
        {
            x = 0;
            y = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2R lhs, Vector2R rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2R a, Vector2R b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R Min(Vector2R lhs, Vector2R rhs)
    {
        return new Vector2R(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R Max(Vector2R lhs, Vector2R rhs)
    {
        return new Vector2R(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator +(Vector2R a, Vector2R b) => new Vector2R(a.x + b.x, a.y + b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator -(Vector2R a, Vector2R b) => new Vector2R(a.x - b.x, a.y - b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator -(Vector2R a) => new Vector2R(-a.x, -a.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator *(Vector2R a, float d) => new Vector2R(a.x * d, a.y * d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator *(float d, Vector2R a) => new Vector2R(a.x * d, a.y * d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2R operator /(Vector2R a, float d) => new Vector2R(a.x / d, a.y / d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2R lhs, Vector2R rhs)
    {
        float dx = lhs.x - rhs.x;
        float dy = lhs.y - rhs.y;
        return dx * dx + dy * dy < 9.99999944E-11f; // Approximately 0.00001
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2R lhs, Vector2R rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object other) => other is Vector2R v && Equals(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector2R other) => this == other;

    public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    public string ToString(string format, IFormatProvider formatProvider)
    {
        if (string.IsNullOrEmpty(format)) format = "F2";
        if (formatProvider == null) formatProvider = CultureInfo.InvariantCulture;
        return $"({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";
    }
}