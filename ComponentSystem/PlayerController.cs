using System.Numerics;
using static Settings;

public class PlayerController : Component, IUpdatable
{
    public Vector2 Direction { get; set; } = new(1, 0);
    public Vector2 CameraPlane => new(-Direction.Y * FOV, Direction.X * FOV);
    public float MoveSpeed { get; init; }
    public float RotationSpeed { get; init; }
    public float MouseRotationSpeed { get; init; }

    public void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement() {  }
    private void HandleRotation() {  }
}