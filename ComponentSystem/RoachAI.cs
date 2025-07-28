using static RoomGenerator;
using System.Numerics;

public class RoachAI : Component, IUpdatable
{
    private HPAStar pathfinder;
    private RoomGenerator roomGenerator;
    private List<Vector2> currentPath = new List<Vector2>();
    private int currentIndex = 0;
    private float idleTimer = 0;
    private const float IdleTime = 1.0f;
    private float moveSpeed = 2.25f;

    private enum State
    {
        Moving,
        Idle
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

        switch (currentState)
        {
            case State.Moving:
                UpdateMovement();
                break;

            case State.Idle:
                UpdateIdle();
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
        if (currentRoom.neighbourRooms.Count > 0 && RandomR.value > 0.7f)
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