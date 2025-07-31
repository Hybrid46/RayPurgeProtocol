using static RoomGenerator;
using System.Numerics;

public class RoachAI : Component, IUpdatable
{
    private const float chanceToStayInRoom = 0.7f; // 30% chance to go to a neighbor room

    private HPAStar pathfinder;
    private RoomGenerator roomGenerator;
    private List<Vector2> currentPath = new List<Vector2>();
    private int currentIndex = 0;
    private float idleTimer = 0;
    private const float IdleTime = 1.0f;
    private float moveSpeed = 2.25f;
    private float attackRange = 0.5f; // Distance for melee attack
    private float attackCooldown = 1.0f; // Time between attacks
    private float attackTimer = 0f;
    private const float VisionDistance = 20f; // Maximum vision distance

    private enum State
    {
        Moving,
        Idle,
        Attacking
    }

    private State currentState = State.Moving;

    public void Initialize(HPAStar pathfinder, RoomGenerator roomGenerator)
    {
        this.roomGenerator = roomGenerator;
        this.pathfinder = pathfinder;
        GetNewRandomTarget();
    }

    public void Update()
    {
        if (pathfinder == null) return;

        // Always check if we should attack
        if (ShouldAttackPlayer())
        {
            currentState = State.Attacking;
            currentPath.Clear();
        }

        switch (currentState)
        {
            case State.Moving:
                UpdateMovement();
                break;

            case State.Idle:
                UpdateIdle();
                break;

            case State.Attacking:
                UpdateAttacking();
                break;
        }
    }

    private void UpdateMovement()
    {
        if (currentPath.Count == 0)
        {
            GetNewRandomTarget();
            return;
        }

        Vector2 targetPos = currentPath[currentIndex];
        Vector2 direction = Vector2.Normalize(targetPos - Entity.transform.Position);
        Vector2 newPos = Entity.transform.Position + direction * moveSpeed * Settings.fixedDeltaTime;

        // Add wall collision check
        if (IsPositionBlocked(newPos))
        {
            currentPath.Clear();
            currentState = State.Idle;
            idleTimer = 0;
            return;
        }

        Entity.transform.Position = newPos;

        if (Vector2.DistanceSquared(Entity.transform.Position, targetPos) < 0.1f)
        {
            currentIndex++;
            if (currentIndex >= currentPath.Count)
            {
                currentState = State.Idle;
                idleTimer = 0;
            }
        }
    }

    private void UpdateIdle()
    {
        idleTimer += Settings.fixedDeltaTime;
        if (idleTimer >= IdleTime)
        {
            // Idle time finished - get new target
            GetNewRandomTarget();
            currentState = State.Moving;
        }
    }

    private bool ShouldAttackPlayer()
    {
        // Always attack if we're already attacking
        if (currentState == State.Attacking) return true;

        Vector2 playerPos = Raycaster.playerEntity.transform.Position;
        Vector2 myPos = Entity.transform.Position;
        float sqrDist = Vector2.DistanceSquared(myPos, playerPos);

        // Check if we're in the same room
        Room myRoom = GetCurrentRoom();
        Room playerRoom = roomGenerator.FindRoomContaining(playerPos);
        if (myRoom != null && playerRoom != null && myRoom == playerRoom) return true;

        // Check vision distance
        if (sqrDist > VisionDistance * VisionDistance) return false;
        // Use DDA for line-of-sight check
        Raycaster.RayHit hit = Raycaster.CastDDA(playerPos - myPos, myPos, roomGenerator.intgrid);
        // Attack if we have direct line of sight to player
        return !hit.IsHit();
    }

    private void UpdateAttacking()
    {
        Vector2 toPlayer = Raycaster.playerEntity.transform.Position - Entity.transform.Position;
        float distance = toPlayer.Length();

        if (distance > attackRange)
        {
            Vector2 direction = Vector2.Normalize(toPlayer);
            Vector2 newPos = Entity.transform.Position + direction * moveSpeed * Settings.fixedDeltaTime;

            if (IsPathBlocked(Entity.transform.Position, newPos) || IsPositionBlocked(newPos))
            {
                if (currentPath.Count == 0 || currentIndex >= currentPath.Count)
                {
                    currentPath = pathfinder.FindPath(Entity.transform.Position, Raycaster.playerEntity.transform.Position);
                    currentIndex = 0;
                }

                if (currentPath.Count > 0)
                {
                    FollowPath();
                }
            }
            else
            {
                Entity.transform.Position = newPos;
            }
        }
        else //attack when in range
        {
            attackTimer += Settings.fixedDeltaTime;
            if (attackTimer >= attackCooldown)
            {
                attackTimer = 0f;
                AttackPlayer();
            }
        }

        // Only stop attacking if player is very far
        if (Vector2.DistanceSquared(Entity.transform.Position, Raycaster.playerEntity.transform.Position) > (VisionDistance * 1.5f) * (VisionDistance * 1.5f))
        {
            currentState = State.Idle;
            idleTimer = 0;
            GetNewRandomTarget();
        }
    }

    private bool IsPositionBlocked(Vector2 position)
    {
        Vector2IntR gridPos = new Vector2IntR(position);
        if (!roomGenerator.IsWithinGrid(gridPos))
            return true; // Out of bounds is blocked

        int tile = roomGenerator.intgrid[gridPos.x, gridPos.y];

        // Block walls (tile 1)
        if (tile == 1) return true;

        // Block closed doors (tile 2 with isOpen=false)
        if (tile == 2)
        {
            if (roomGenerator.doorPositionMap.TryGetValue(gridPos, out Door door))
            {
                return !door.isOpen; // Block if door is closed
            }
            return true; // Block if door not found (shouldn't happen)
        }

        return false; // All other tiles are passable
    }

    private bool IsPathBlocked(Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        Raycaster.RayHit hit = Raycaster.CastDDA(dir, start, roomGenerator.intgrid);
        return hit.IsHit() && hit.distance < dir.Length();
    }

    private void FollowPath()
    {
        if (currentIndex < currentPath.Count)
        {
            Vector2 targetPos = currentPath[currentIndex];
            Vector2 direction = Vector2.Normalize(targetPos - Entity.transform.Position);
            Vector2 newPos = Entity.transform.Position + direction * moveSpeed * Settings.fixedDeltaTime;

            if (IsPositionBlocked(newPos))
            {
                currentPath.Clear();
                currentState = State.Idle;
                idleTimer = 0;
                return;
            }

            Entity.transform.Position = newPos;

            if (Vector2.DistanceSquared(Entity.transform.Position, targetPos) < 0.1f)
            {
                currentIndex++;
            }
        }
    }

    private void AttackPlayer()
    {
        // Damage player
        HealthComponent playerHealth = Raycaster.playerEntity?.healthComponent;
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            Console.WriteLine("Roach attacked player! Player health: " + playerHealth.CurrentHP);
        }
    }

    private void GetNewRandomTarget()
    {
        Vector2 target = GetRandomPositionInNeighborRoom();
        currentPath = pathfinder.FindPath(Entity.transform.Position, target);
        currentIndex = 0;

        // If no path found, try again next frame
        if (currentPath.Count == 0)
        {
            currentState = State.Idle;
            idleTimer = IdleTime - 0.1f; // Try again very soon
        }
    }

    private Vector2 GetRandomPositionInNeighborRoom()
    {
        Room currentRoom = GetCurrentRoom();
        if (currentRoom == null) return Entity.transform.Position; // Fallback to current position

        // 70% chance to stay in current room, 30% to move to neighbor room
        Room targetRoom = currentRoom;
        if (currentRoom.neighbourRooms.Count > 0 && RandomR.value > chanceToStayInRoom)
        {
            // Get random neighbor room
            int randomIndex = RandomR.Range(0, currentRoom.neighbourRooms.Count);
            targetRoom = currentRoom.neighbourRooms.ElementAt(randomIndex);
        }

        // Get random position within the room
        int randomCoordIndex = RandomR.Range(0, targetRoom.coords.Count);
        Vector2IntR randomCoord = targetRoom.coords.ElementAt(randomCoordIndex);

        // Return center of the tile
        return new Vector2(randomCoord.x + 0.5f, randomCoord.y + 0.5f);
    }

    private Room GetCurrentRoom()
    {
        Vector2IntR gridPos = new Vector2IntR(Entity.transform.Position);

        return roomGenerator.FindRoomContaining(gridPos);
    }
}