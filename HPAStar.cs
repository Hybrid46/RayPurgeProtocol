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