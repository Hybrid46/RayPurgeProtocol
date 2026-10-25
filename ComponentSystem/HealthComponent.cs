public class HealthComponent : Component
{
    public int max { get; private set; }
    public int current { get; private set; }

    public HealthComponent(int max)
    {
        this.max = max;
        current = max;
    }

    public void TakeDamage(int amount)
    {
        current = Math.Clamp(current - amount, 0 , max);
        if (current == 0) Die();
    }

    protected virtual void Die()
    {
        Entity.Destroy();
        Console.WriteLine($"Entity {GetHashCode()} died!");
    }
}
