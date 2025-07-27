using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

public struct Vector2IntR : IEquatable<Vector2IntR>, IFormattable
{
    private int m_X;
    private int m_Y;

    private static readonly Vector2IntR s_Zero = new Vector2IntR(0, 0);
    private static readonly Vector2IntR s_One = new Vector2IntR(1, 1);
    private static readonly Vector2IntR s_Up = new Vector2IntR(0, 1);
    private static readonly Vector2IntR s_Down = new Vector2IntR(0, -1);
    private static readonly Vector2IntR s_Left = new Vector2IntR(-1, 0);
    private static readonly Vector2IntR s_Right = new Vector2IntR(1, 0);

    public int x
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => m_X;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => m_X = value;
    }

    public int y
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => m_Y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => m_Y = value;
    }

    public int this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => index switch
        {
            0 => x,
            1 => y,
            _ => throw new IndexOutOfRangeException($"Invalid Vector2Int index: {index}!")
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (index)
            {
                case 0: x = value; break;
                case 1: y = value; break;
                default: throw new IndexOutOfRangeException($"Invalid Vector2Int index: {index}!");
            }
        }
    }

    public float magnitude => (float)Math.Sqrt(sqrMagnitude);
    public int sqrMagnitude => x * x + y * y;

    public static Vector2IntR zero => s_Zero;
    public static Vector2IntR one => s_One;
    public static Vector2IntR up => s_Up;
    public static Vector2IntR down => s_Down;
    public static Vector2IntR left => s_Left;
    public static Vector2IntR right => s_Right;

    public Vector2IntR(int x, int y)
    {
        m_X = x;
        m_Y = y;
    }

    public Vector2IntR(Vector2 v)
    {
        m_X = (int)v.X;
        m_Y = (int)v.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int x, int y)
    {
        m_X = x;
        m_Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2IntR a, Vector2IntR b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR Min(Vector2IntR lhs, Vector2IntR rhs) =>
        new Vector2IntR(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR Max(Vector2IntR lhs, Vector2IntR rhs) =>
        new Vector2IntR(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR Scale(Vector2IntR a, Vector2IntR b) =>
        new Vector2IntR(a.x * b.x, a.y * b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Scale(Vector2IntR scale)
    {
        x *= scale.x;
        y *= scale.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clamp(Vector2IntR min, Vector2IntR max)
    {
        x = Math.Max(min.x, Math.Min(max.x, x));
        y = Math.Max(min.y, Math.Min(max.y, y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR FloorToInt(Vector2R v) =>
        new Vector2IntR((int)Math.Floor(v.x), (int)Math.Floor(v.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR CeilToInt(Vector2R v) =>
        new Vector2IntR((int)Math.Ceiling(v.x), (int)Math.Ceiling(v.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR RoundToInt(Vector2R v) =>
        new Vector2IntR((int)Math.Round(v.x), (int)Math.Round(v.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2R(Vector2IntR v) =>
        new Vector2R(v.x, v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator -(Vector2IntR v) => new Vector2IntR(-v.x, -v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator +(Vector2IntR a, Vector2IntR b) =>
        new Vector2IntR(a.x + b.x, a.y + b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator -(Vector2IntR a, Vector2IntR b) =>
        new Vector2IntR(a.x - b.x, a.y - b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator *(Vector2IntR a, Vector2IntR b) =>
        new Vector2IntR(a.x * b.x, a.y * b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator *(int a, Vector2IntR b) =>
        new Vector2IntR(a * b.x, a * b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator *(Vector2IntR a, int b) =>
        new Vector2IntR(a.x * b, a.y * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2IntR operator /(Vector2IntR a, int b) =>
        new Vector2IntR(a.x / b, a.y / b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2IntR lhs, Vector2IntR rhs) =>
        lhs.x == rhs.x && lhs.y == rhs.y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2IntR lhs, Vector2IntR rhs) =>
        !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object other) =>
        other is Vector2IntR v && Equals(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector2IntR other) =>
        x == other.x && y == other.y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() =>
        (x * 73856093) ^ (y * 83492791);

    public override string ToString() =>
        ToString(null, null);

    public string ToString(string format) =>
        ToString(format, null);

    public string ToString(string format, IFormatProvider formatProvider)
    {
        if (formatProvider == null)
            formatProvider = CultureInfo.InvariantCulture;

        return $"({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";
    }
}