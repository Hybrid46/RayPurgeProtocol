public class HealthComponent : Component
{
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public HealthComponent(int maxHP)
    {
        MaxHP = maxHP;
        CurrentHP = maxHP;
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Math.Max(0, CurrentHP - amount);
        if (CurrentHP == 0) Die();
    }

    protected virtual void Die()
    {
        Entity.Destroy();
        Console.WriteLine($"Entity {GetHashCode()} died!");
    }
}
