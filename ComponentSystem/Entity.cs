using System;
using System.Collections.Generic;

public class Entity
{
    private readonly Dictionary<Type, Component> _components = new();
    private Transform _cachedTransform;
    private RaySpriteRenderer _cachedRaySpriteRenderer;
    private HealthComponent _cachedHealthComponent;

    public Transform transform => _cachedTransform;
    public RaySpriteRenderer raySpriteRenderer => _cachedRaySpriteRenderer;
    public HealthComponent healthComponent => _cachedHealthComponent;

    public T AddComponent<T>(T component) where T : Component
    {
        component.Entity = this;
        _components[typeof(T)] = component;

        // Update cache
        if (component is Transform transform) _cachedTransform = transform;
        if (component is RaySpriteRenderer raySpriteRenderer) _cachedRaySpriteRenderer = raySpriteRenderer;
        if (component is HealthComponent healthComponent) _cachedHealthComponent = healthComponent;

        return component;
    }

    public T GetComponent<T>() where T : Component
    {
        // Special handling for Transform
        if (typeof(T) == typeof(Transform)) return _cachedTransform as T;
        if (typeof(T) == typeof(RaySpriteRenderer)) return _cachedRaySpriteRenderer as T;
        if (typeof(T) == typeof(HealthComponent)) return _cachedHealthComponent as T;

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
}
