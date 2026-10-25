using PurgeProtocol.ComponentSystem;

public class Entity
{
    private readonly Dictionary<Type, Component> _components = new();
    private Transform _cachedTransform;
    private RaySpriteRenderer _cachedRaySpriteRenderer;
    private HealthComponent _cachedHealthComponent;
    private ManaComponent _cachedManaComponent;
    private MovementComponent _cachedMovementComponent;
    private PlayerController _cachedPlayerController;
    private PowerUpComponent _cachedPowerUpComponent;

    public bool destroy { get; private set; } = false;

    public Transform transform => _cachedTransform;
    public RaySpriteRenderer raySpriteRenderer => _cachedRaySpriteRenderer;
    public HealthComponent healthComponent => _cachedHealthComponent;
    public ManaComponent manaComponent => _cachedManaComponent;
    public MovementComponent movementComponent => _cachedMovementComponent;
    public PlayerController playerController => _cachedPlayerController;
    public PowerUpComponent powerUpComponent => _cachedPowerUpComponent;

    public T AddComponent<T>(T component) where T : Component
    {
        component.Entity = this;
        _components[typeof(T)] = component;

        // Update cache
        if (component is Transform transform) _cachedTransform = transform;
        if (component is RaySpriteRenderer raySpriteRenderer) _cachedRaySpriteRenderer = raySpriteRenderer;
        if (component is HealthComponent healthComponent) _cachedHealthComponent = healthComponent;
        if (component is ManaComponent manaComponent) _cachedManaComponent = manaComponent;
        if (component is BulletHealthComponent bulletHealthComponent) _cachedHealthComponent = bulletHealthComponent;
        if (component is MovementComponent movement) _cachedMovementComponent = movement;
        if (component is PlayerController playerController) _cachedPlayerController = playerController;
        if (component is PowerUpComponent powerUpComponent) _cachedPowerUpComponent = powerUpComponent;

        return component;
    }

    public T GetComponent<T>() where T : Component
    {
        // Special handling for Transform
        if (typeof(T) == typeof(Transform)) return _cachedTransform as T;
        if (typeof(T) == typeof(RaySpriteRenderer)) return _cachedRaySpriteRenderer as T;
        if (typeof(T) == typeof(HealthComponent)) return _cachedHealthComponent as T;
        if (typeof(T) == typeof(ManaComponent)) return _cachedManaComponent as T;
        if (typeof(T) == typeof(BulletHealthComponent)) return _cachedHealthComponent as T;
        if (typeof(T) == typeof(MovementComponent)) return _cachedMovementComponent as T;
        if (typeof(T) == typeof(PlayerController)) return _cachedPlayerController as T;
        if (typeof(T) == typeof(PowerUpComponent)) return _cachedPowerUpComponent as T;

        return _components.TryGetValue(typeof(T), out var component)
            ? (T)component
            : null;
    }

    public void Update()
    {
        // Update all updatable components
        foreach (var component in _components.Values.OfType<IUpdatable>())
        {
            component.Update();
        }
    }

    public void Destroy()
    {
        destroy = true;
        //Console.WriteLine($"Entity {GetHashCode()} destroyed");
    }
}
