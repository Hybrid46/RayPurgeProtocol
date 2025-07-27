using static RoomGenerator;
using System.Numerics;
using System.IO;
using System;

public class HPAStar
{
    private RoomGenerator roomGenerator;
    private Dictionary<Room, Cluster> clusters = new Dictionary<Room, Cluster>();
    private Dictionary<Vector2IntR, DoorNode> doorNodes = new Dictionary<Vector2IntR, DoorNode>();

    public HPAStar(RoomGenerator roomGenerator)
    {
        this.roomGenerator = roomGenerator;

        // Create clusters and door nodes
        foreach (Room room in roomGenerator.rooms)
        {
            Cluster cluster = new Cluster(room);
            clusters.Add(room, cluster);

            foreach (Door door in room.doors)
            {
                if (!doorNodes.ContainsKey(door.position))
                {
                    DoorNode node = new DoorNode(door.position);
                    doorNodes.Add(door.position, node);
                    node.Clusters.Add(cluster);
                }
                else
                {
                    doorNodes[door.position].Clusters.Add(cluster);
                }
            }
        }

        // Connect door nodes within clusters
        foreach (Cluster cluster in clusters.Values)
        {
            cluster.DoorNodes = cluster.Room.doors
                          .Select(door => doorNodes[door.position])
                          .ToList();

            // Connect all doors within the same cluster
            for (int i = 0; i < cluster.DoorNodes.Count; i++)
            {
                for (int j = i + 1; j < cluster.DoorNodes.Count; j++)
                {
                    DoorNode a = cluster.DoorNodes[i];
                    DoorNode b = cluster.DoorNodes[j];
                    float distance = Vector2.Distance(a.Position, b.Position);
                    a.AddNeighbor(b, distance);
                    b.AddNeighbor(a, distance);
                }
            }
        }
    }

    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        Room startRoom = roomGenerator.FindRoomContaining(start);
        Room goalRoom = roomGenerator.FindRoomContaining(goal);

        if (startRoom == null || goalRoom == null) return new List<Vector2>();

        // Create temporary nodes
        PositionNode startNode = new PositionNode(start);
        PositionNode goalNode = new PositionNode(goal);

        // Always connect start to doors in its room (no raycast needed)
        foreach (DoorNode door in clusters[startRoom].DoorNodes)
        {
            float cost = Vector2.Distance(start, door.Position);
            startNode.AddNeighbor(door, cost);
        }

        // Always connect goal to doors in its room (no raycast needed)
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

// Simple Priority Queue
public class PriorityQueue<T>
{
    private SortedDictionary<float, Queue<T>> _dict = new SortedDictionary<float, Queue<T>>();

    public int Count { get; private set; }

    public void Enqueue(T item, float priority)
    {
        if (!_dict.ContainsKey(priority)) _dict[priority] = new Queue<T>();

        _dict[priority].Enqueue(item);
        Count++;
    }

    public T Dequeue()
    {
        var first = _dict.First();
        var item = first.Value.Dequeue();

        if (first.Value.Count == 0) _dict.Remove(first.Key);
        Count--;

        return item;
    }
}