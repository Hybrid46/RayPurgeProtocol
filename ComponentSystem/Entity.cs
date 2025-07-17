using System;
using System.Collections.Generic;

public class Entity
{
    private readonly Dictionary<Type, Component> _components = new();
    private Transform _cachedTransform;

    public Transform transform => _cachedTransform;

    public T AddComponent<T>(T component) where T : Component
    {
        component.Entity = this;
        _components[typeof(T)] = component;

        // Update cache when adding Transform
        if (component is Transform transform)
        {
            _cachedTransform = transform;
        }

        return component;
    }

    public T GetComponent<T>() where T : Component
    {
        // Special handling for Transform
        if (typeof(T) == typeof(Transform))
        {
            return _cachedTransform as T;
        }

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
