using System;
using System.Collections.Generic;
using System.Numerics;

public class Transform : Component
{
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public float Scale { get; set; } = 1f;
}