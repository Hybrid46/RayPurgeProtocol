using Raylib_cs;
using System.Numerics;
using System;
using System.Collections.Generic;

public class SpriteRenderer : Component
{
    public Texture2D Texture { get; set; }
    public Vector2 Position => Entity.GetComponent<Transform>().Position;

    public void Draw()
    {
        //Draw
    }
}