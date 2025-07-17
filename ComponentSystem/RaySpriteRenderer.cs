using Raylib_cs;
using System.Numerics;
using System;
using System.Collections.Generic;

public class RaySpriteRenderer : Component
{
    public Texture2D Texture { get; set; }
    public Vector2 Position => Entity.transform.Position;

    public void Draw()
    {
        throw new NotImplementedException("RaySpriteDraw not implemented yet!");
    }
}