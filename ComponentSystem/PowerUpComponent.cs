namespace PurgeProtocol.ComponentSystem
{
    public class PowerUpComponent : Component
    {
        public int effectType; // 0 hp,1 mana, 2 endgameitem

        public void PickUp(Entity entity)
        {
            switch (effectType)
            {
                case 0:
                    HealthComponent healthComponent = entity.GetComponent<HealthComponent>();
                    if (healthComponent != null)
                    {
                        if (healthComponent.current < healthComponent.max)
                        {
                            healthComponent.TakeDamage(-5);
                            Console.WriteLine($"Entity {GetHashCode()} picked up, +5 HP");
                        }
                    }
                    else return;
                    break;
                case 1:
                    ManaComponent manaComponent = entity.manaComponent;
                    if (manaComponent != null)
                    {
                        if (manaComponent.current < manaComponent.max)
                        {
                            manaComponent.TakeMana(-5);
                            Console.WriteLine($"Entity {GetHashCode()} picked up, +5 mana");
                        }
                    }
                    else return;
                    break;
                case 2: // endgame item triggers win + main menu
                    Console.WriteLine($"Entity {GetHashCode()} picked up, endgame item");
                    Console.WriteLine($"You won the game!");
                    GameSession.EndGame(true);
                    break;
                default:
                    break;
            }

            Entity.Destroy();
            Console.WriteLine($"Entity {GetHashCode()} destroyed");
        }
    }
}