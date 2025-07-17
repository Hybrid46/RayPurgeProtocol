using System;
using System.Collections.Generic;
using System.Numerics;
public class RoachAI : Component, IUpdatable
{
    private Transform transform;
    private float moveSpeed = 50f;
    public Vector2 targetPosition;

    public void Update()
    {
        transform ??= Entity.GetComponent<Transform>();

        if (Vector2.Distance(transform.Position, targetPosition) > 1f)
        {
            Vector2 direction = Vector2.Normalize(targetPosition - transform.Position);
            transform.Position += direction * moveSpeed * Settings.fixedDeltaTime;
        }
    }
}
