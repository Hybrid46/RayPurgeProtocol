using System.Numerics;

public class MovementComponent : Component, IUpdatable
{
    public Vector2 direction { get; set; }
    public float speed { get; set; }
    public float acceleration { get; set; }
    public float maxSpeed { get; set; }
    public float minSpeed { get; set; }
    public bool shouldDecayWhenZeroSpeed { get; set; }

    public MovementComponent(Vector2 direction, float initialSpeed, float acceleration = 0f,
                            float maxSpeed = float.MaxValue, float minSpeed = 0f,
                            bool shouldDecay = false)
    {
        this.direction = direction;
        speed = initialSpeed;
        this.acceleration = acceleration;
        this.maxSpeed = maxSpeed;
        this.minSpeed = minSpeed;
        this.shouldDecayWhenZeroSpeed = shouldDecay;
    }

    public void Update()
    {
        speed += acceleration;
        speed = Math.Clamp(speed, minSpeed, maxSpeed);

        Vector2 movement = direction * speed * Settings.fixedDeltaTime;

        // Apply movement to transform
        if (Entity.transform != null)
        {
            Entity.transform.Position += movement;
        }

        // Decay to zero and destroy when minimum speed is reached
        if (shouldDecayWhenZeroSpeed && Math.Abs(speed) <= float.Epsilon)
        {
            Entity.Destroy();
        }
    }
}