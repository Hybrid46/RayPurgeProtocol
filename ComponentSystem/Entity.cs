using System;
using System.Collections.Generic;

public class Entity
{
    private readonly Dictionary<Type, Component> _components = new();

    public T AddComponent<T>(T component) where T : Component
    {
        component.Entity = this;
        _components[typeof(T)] = component;
        return component;
    }

    public T GetComponent<T>() where T : Component
    {
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
