using System;
using System.Collections.Generic;
using System.Numerics;
public class RoachAI : Component, IUpdatable
{
    public enum State
    {
        Idle,
        Moving,
        Attacking,
        Fleeing
    }

    public State CurrentState { get; private set; } = State.Idle;
    private float moveSpeed = 0.25f;
    public Vector2 targetPosition;
    
    public void Update()
    {
        Transform transform = Entity.GetComponent<Transform>();

        if (Vector2.Distance(transform.Position, targetPosition) > 1f)
        {
            CurrentState = State.Moving;
            Vector2 direction = Vector2.Normalize(targetPosition - transform.Position);
            transform.Position += direction * moveSpeed * Settings.fixedDeltaTime;
        }
    }
}
