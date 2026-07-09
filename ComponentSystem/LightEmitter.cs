using Raylib_cs;

namespace PurgeProtocol.ComponentSystem
{
    public class LightEmitter : Component
    {
        public int width { get; private set; }
        public int height { get; private set; }
        public Color color { get; private set; }
        public bool isCircle { get; private set; }
        public float radius { get; private set; }

        private LightEmitter() { }

        public LightEmitter(int width, int height, Color color)
        {
            this.width = width;
            this.height = height;
            this.color = color;
            this.isCircle = false;
            this.radius = 0f;
        }

        public LightEmitter(float radius, Color color)
        {
            this.width = 0;
            this.height = 0;
            this.color = color;
            this.isCircle = true;
            this.radius = radius;
        }
    }
}
