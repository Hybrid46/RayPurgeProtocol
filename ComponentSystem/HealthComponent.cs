using System;
using System.Collections.Generic;

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

    private void Die()
    {
        // Handle death
        Console.WriteLine("Enemy died!");
    }
}
