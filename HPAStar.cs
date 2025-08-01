using static RoomGenerator;
using System.Numerics;

public class HPAStar
{
    private RoomGenerator roomGenerator;
    private Dictionary<Room, Cluster> clusters = new Dictionary<Room, Cluster>();
    private Dictionary<Vector2IntR, DoorNode> doorNodes = new Dictionary<Vector2IntR, DoorNode>();

    public HPAStar(RoomGenerator roomGenerator)
    {
        this.roomGenerator = roomGenerator;

        // Create clusters for each room
        foreach (Room room in roomGenerator.rooms)
        {
            Cluster cluster = new Cluster(room);
            clusters.Add(room, cluster);
        }

        // Create door nodes and properly associate them with clusters
        foreach (Room room in roomGenerator.rooms)
        {
            foreach (Door door in room.doors)
            {
                if (!doorNodes.ContainsKey(door.position))
                {
                    DoorNode node = new DoorNode(door.position);
                    doorNodes.Add(door.position, node);

                    // Properly associate this door with all clusters it belongs to
                    if (door.roomA != null && clusters.ContainsKey(door.roomA))
                        node.Clusters.Add(clusters[door.roomA]);
                    if (door.roomB != null && clusters.ContainsKey(door.roomB))
                        node.Clusters.Add(clusters[door.roomB]);
                }
                else
                {
                    // If door node already exists, add the cluster association
                    if (door.roomA != null && clusters.ContainsKey(door.roomA))
                        doorNodes[door.position].Clusters.Add(clusters[door.roomA]);
                    if (door.roomB != null && clusters.ContainsKey(door.roomB))
                        doorNodes[door.position].Clusters.Add(clusters[door.roomB]);
                }
            }
        }

        // Connect door nodes within clusters
        foreach (Cluster cluster in clusters.Values)
        {
            List<DoorNode> clusterDoors = new List<DoorNode>();

            foreach (Door door in cluster.Room.doors)
            {
                if (doorNodes.ContainsKey(door.position))
                {
                    clusterDoors.Add(doorNodes[door.position]);
                }
            }

            cluster.DoorNodes = clusterDoors;

            // Connect all doors within the same cluster
            for (int i = 0; i < clusterDoors.Count; i++)
            {
                for (int j = i + 1; j < clusterDoors.Count; j++)
                {
                    DoorNode a = clusterDoors[i];
                    DoorNode b = clusterDoors[j];
                    float distance = Vector2.Distance(a.Position, b.Position);
                    a.AddNeighbor(b, distance);
                    b.AddNeighbor(a, distance);
                }
            }
        }
    }


    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        Room startRoom = roomGenerator.GetRoomAtPosition(start);
        Room goalRoom = roomGenerator.GetRoomAtPosition(goal);

        if (startRoom == null || goalRoom == null) return new List<Vector2>();

        // Create temporary nodes
        PositionNode startNode = new PositionNode(start);
        PositionNode goalNode = new PositionNode(goal);

        // Always connect start to doors in its room
        foreach (DoorNode door in clusters[startRoom].DoorNodes)
        {
            float cost = Vector2.Distance(start, door.Position);
            startNode.AddNeighbor(door, cost);
        }

        // Always connect goal to doors in its room
        foreach (DoorNode door in clusters[goalRoom].DoorNodes)
        {
            float cost = Vector2.Distance(goal, door.Position);
            door.AddNeighbor(goalNode, cost);
        }

        // A* search
        return AStarSearch(startNode, goalNode);
    }

    private List<Vector2> AStarSearch(PathNode start, PathNode goal)
    {
        PriorityQueue<PathNode> openSet = new PriorityQueue<PathNode>();
        Dictionary<PathNode, PathNode> cameFrom = new Dictionary<PathNode, PathNode>();
        Dictionary<PathNode, float> gScore = new Dictionary<PathNode, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;

        while (openSet.Count > 0)
        {
            PathNode current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in current.Neighbors)
            {
                float tentativeG = gScore[current] + neighbor.Value;

                if (!gScore.ContainsKey(neighbor.Key) || tentativeG < gScore[neighbor.Key])
                {
                    cameFrom[neighbor.Key] = current;
                    gScore[neighbor.Key] = tentativeG;
                    float fScore = tentativeG + Vector2.Distance(neighbor.Key.Position, goal.Position);
                    openSet.Enqueue(neighbor.Key, fScore);
                }
            }
        }

        return new List<Vector2>(); // No path found
    }

    private List<Vector2> ReconstructPath(Dictionary<PathNode, PathNode> cameFrom, PathNode current)
    {
        List<Vector2> path = new List<Vector2>();

        while (cameFrom.ContainsKey(current))
        {
            path.Add(current.Position);
            current = cameFrom[current];
        }

        path.Reverse();

        return path;
    }
}

public class Cluster
{
    public Room Room;
    public List<DoorNode> DoorNodes = new List<DoorNode>();

    public Cluster(Room room)
    {
        Room = room;
    }
}

public abstract class PathNode
{
    public Vector2 Position { get; protected set; }
    public Dictionary<PathNode, float> Neighbors { get; } = new Dictionary<PathNode, float>();

    public void AddNeighbor(PathNode node, float cost)
    {
        Neighbors[node] = cost;
    }
}

public class DoorNode : PathNode
{
    public Vector2IntR DoorPosition;
    public List<Cluster> Clusters = new List<Cluster>();

    public DoorNode(Vector2IntR position)
    {
        DoorPosition = position;
        Position = new Vector2(position.x + 0.5f, position.y + 0.5f);
    }
}

public class PositionNode : PathNode
{
    public PositionNode(Vector2 position)
    {
        Position = position;
    }
}

public class PriorityQueue<T>
{
    private List<(float Priority, T Item)> _heap = new List<(float, T)>();
    private Dictionary<T, int> _itemIndices = new Dictionary<T, int>();

    public int Count => _heap.Count;

    public void Enqueue(T item, float priority)
    {
        _heap.Add((priority, item));
        int childIndex = _heap.Count - 1;
        _itemIndices[item] = childIndex;  // Update index tracking

        // Heapify-up
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2;
            if (_heap[parentIndex].Priority <= _heap[childIndex].Priority)
                break;

            Swap(parentIndex, childIndex);
            childIndex = parentIndex;
        }
    }

    public T Dequeue()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        T result = _heap[0].Item;
        RemoveIndex(result);  // Remove index tracking

        int lastIdx = _heap.Count - 1;
        _heap[0] = _heap[lastIdx];
        _heap.RemoveAt(lastIdx);

        if (_heap.Count > 0)
        {
            UpdateIndex(0);  // Update index tracking
            HeapifyDown(0);
        }

        return result;
    }

    public bool TryDequeue(out T item, out float priority)
    {
        if (Count == 0)
        {
            item = default;
            priority = default;
            return false;
        }

        priority = _heap[0].Priority;
        item = Dequeue();
        return true;
    }

    public void UpdatePriority(T item, float newPriority)
    {
        if (!_itemIndices.TryGetValue(item, out int index))
            throw new ArgumentException("Item not in queue");

        float oldPriority = _heap[index].Priority;
        _heap[index] = (newPriority, item);

        if (newPriority < oldPriority)
            HeapifyUp(index);
        else
            HeapifyDown(index);
    }

    public bool Contains(T item) => _itemIndices.ContainsKey(item);

    public void Clear()
    {
        _heap.Clear();
        _itemIndices.Clear();
    }

    private void HeapifyUp(int childIndex)
    {
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2;
            if (_heap[parentIndex].Priority <= _heap[childIndex].Priority)
                break;

            Swap(parentIndex, childIndex);
            childIndex = parentIndex;
        }
    }

    private void HeapifyDown(int parentIndex)
    {
        while (true)
        {
            int leftChild = 2 * parentIndex + 1;
            int rightChild = 2 * parentIndex + 2;
            int smallest = parentIndex;

            if (leftChild < _heap.Count && _heap[leftChild].Priority < _heap[smallest].Priority)
                smallest = leftChild;

            if (rightChild < _heap.Count && _heap[rightChild].Priority < _heap[smallest].Priority)
                smallest = rightChild;

            if (smallest == parentIndex) break;

            Swap(parentIndex, smallest);
            parentIndex = smallest;
        }
    }

    private void Swap(int indexA, int indexB)
    {
        (_heap[indexA], _heap[indexB]) = (_heap[indexB], _heap[indexA]);
        UpdateIndex(indexA);
        UpdateIndex(indexB);
    }

    // Index tracking helpers
    private void UpdateIndex(int index) => _itemIndices[_heap[index].Item] = index;
    private void RemoveIndex(T item) => _itemIndices.Remove(item);
}