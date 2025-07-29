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

        CheckForPlayer();

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

        Entity.transform.Position += direction * moveSpeed * Settings.fixedDeltaTime;

        if (Vector2.DistanceSquared(Entity.transform.Position, targetPos) < 0.1f)
        {
            currentIndex++;
            if (currentIndex >= currentPath.Count)
            {
                // Reached final target - go idle
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

    private void CheckForPlayer()
    {
        // Check if we're in the same room as player
        Room currentRoom = GetCurrentRoom();
        Room playerRoom = roomGenerator.FindRoomContaining(Raycaster.playerEntity.transform.Position);

        bool inSameRoom = currentRoom == playerRoom;
        bool hasLineOfSight = inSameRoom || HasLineOfSight(Entity.transform.Position, Raycaster.playerEntity.transform.Position);

        // Switch to attacking if we see player and are close enough
        if (hasLineOfSight && Vector2.Distance(Entity.transform.Position, Raycaster.playerEntity.transform.Position) <= VisionDistance)
        {
            currentState = State.Attacking;
            currentPath.Clear(); // Clear any existing path
        }
    }

    private void UpdateAttacking()
    {
        Vector2 toPlayer = Raycaster.playerEntity.transform.Position - Entity.transform.Position;
        float distance = toPlayer.Length();

        // Move towards player if not in attack range
        if (distance > attackRange)
        {
            Vector2 direction = Vector2.Normalize(toPlayer);
            Entity.transform.Position += direction * moveSpeed * Settings.fixedDeltaTime;
        }
        else // Attack when in range
        {
            attackTimer += Settings.fixedDeltaTime;
            if (attackTimer >= attackCooldown)
            {
                attackTimer = 0f;
                AttackPlayer();
            }
        }

        // Check if we lost sight of player
        if (!HasLineOfSight(Entity.transform.Position, Raycaster.playerEntity.transform.Position) ||
            Vector2.Distance(Entity.transform.Position, Raycaster.playerEntity.transform.Position) > VisionDistance)
        {
            currentState = State.Idle;
            idleTimer = 0;
            GetNewRandomTarget();
        }
    }

    private void AttackPlayer()
    {
        // Damage player
        HealthComponent playerHealth = Raycaster.playerEntity?.GetComponent<HealthComponent>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            Console.WriteLine("Roach attacked player! Player health: " + playerHealth.CurrentHP);
        }
    }

    private bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float distance = direction.Length();
        direction = Vector2.Normalize(direction);

        int steps = (int)(distance * 2); // 2 checks per unit
        int[,] map = roomGenerator.intgrid;

        for (int i = 0; i <= steps; i++)
        {
            Vector2 pos = start + direction * (distance * i / steps);
            int x = (int)pos.X;
            int y = (int)pos.Y;

            // Out of bounds
            if (x < 0 || x >= map.GetLength(0) || y < 0 || y >= map.GetLength(1))
                return false;

            // Hit a wall
            if (map[x, y] == 1)
                return false;

            // Hit a closed door
            if (map[x, y] == 2)
            {
                Vector2IntR doorPos = new Vector2IntR(x, y);
                if (roomGenerator.doorPositionMap.TryGetValue(doorPos, out Door door) && !door.isOpen)
                    return false;
            }
        }

        return true;
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