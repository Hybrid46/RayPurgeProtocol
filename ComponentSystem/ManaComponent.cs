namespace PurgeProtocol.ComponentSystem
{
    public class ManaComponent : Component
    {
        public int max { get; private set; }
        public int current { get; private set; }

        public ManaComponent(int max)
        {
            this.max = max;
            current = max;
        }

        public void TakeMana(int amount)
        {
            current = Math.Clamp(current - amount, 0, max);
        }
    }

}
