using static RoomGenerator;
using System.Numerics;

public class RoachAI : Component, IUpdatable
{
    private HPAStar _pathfinder;
    private List<Vector2> _currentPath = new List<Vector2>();
    private int _currentIndex = 0;
    private float _idleTimer = 0;
    private const float IdleTime = 1.0f;
    private float moveSpeed = 2.25f;

    private enum State
    {
        Moving,
        Idle
    }

    private State _currentState = State.Moving;

    public void Initialize(HPAStar pathfinder)
    {
        _pathfinder = pathfinder;
        GetNewRandomTarget();
    }

    public void Update()
    {
        if (_pathfinder == null) return;

        switch (_currentState)
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
        if (_currentPath.Count == 0)
        {
            GetNewRandomTarget();
            return;
        }

        Vector2 targetPos = _currentPath[_currentIndex];
        Vector2 direction = Vector2.Normalize(targetPos - Entity.transform.Position);

        Entity.transform.Position += direction * moveSpeed * Settings.fixedDeltaTime;

        if (Vector2.DistanceSquared(Entity.transform.Position, targetPos) < 0.1f)
        {
            _currentIndex++;
            if (_currentIndex >= _currentPath.Count)
            {
                // Reached final target - go idle
                _currentState = State.Idle;
                _idleTimer = 0;
            }
        }
    }

    private void UpdateIdle()
    {
        _idleTimer += Settings.fixedDeltaTime;
        if (_idleTimer >= IdleTime)
        {
            // Idle time finished - get new target
            GetNewRandomTarget();
            _currentState = State.Moving;
        }
    }

    private void GetNewRandomTarget()
    {
        Vector2 target = GetRandomPositionInNeighborRoom();
        _currentPath = _pathfinder.FindPath(Entity.transform.Position, target);
        _currentIndex = 0;

        // If no path found, try again next frame
        if (_currentPath.Count == 0)
        {
            _currentState = State.Idle;
            _idleTimer = IdleTime - 0.1f; // Try again very soon
        }
    }

    private Vector2 GetRandomPositionInNeighborRoom()
    {
        Room currentRoom = GetCurrentRoom();
        if (currentRoom == null)
            return Entity.transform.Position; // Fallback to current position

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
        Vector2IntR gridPos = new Vector2IntR(
            (int)Entity.transform.Position.X,
            (int)Entity.transform.Position.Y
        );
        return _pathfinder.FindRoomContaining(Entity.transform.Position);
    }
}