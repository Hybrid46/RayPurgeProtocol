using System.Numerics;
using static RoomGenerator;

public class AStarPathfinder
{
    private RoomGenerator roomGenerator;

    public AStarPathfinder(RoomGenerator roomGenerator)
    {
        this.roomGenerator = roomGenerator;
    }

    public List<Vector2> FindPath(Vector2 start, Vector2 end)
    {
        Vector2IntR startGrid = new Vector2IntR(start);
        Vector2IntR endGrid = new Vector2IntR(end);

        if (!roomGenerator.IsWithinGrid(startGrid) || !roomGenerator.IsWithinGrid(endGrid))
            return new List<Vector2>();

        // Custom priority queue for A* nodes
        PriorityQueue<AStarNode> openSet = new PriorityQueue<AStarNode>();
        Dictionary<Vector2IntR, AStarNode> allNodes = new Dictionary<Vector2IntR, AStarNode>();
        HashSet<Vector2IntR> closedSet = new HashSet<Vector2IntR>();

        // Create start node
        AStarNode startNode = new AStarNode(startGrid, 0, Heuristic(startGrid, endGrid), null);
        openSet.Enqueue(startNode, startNode.F);
        allNodes.Add(startGrid, startNode);

        while (openSet.Count > 0)
        {
            AStarNode current = openSet.Dequeue();

            // Path found
            if (current.Position == endGrid)
            {
                return ReconstructPath(current);
            }

            closedSet.Add(current.Position);

            // Check all 4-direction neighbors
            foreach (Vector2IntR neighborPos in GetNeighbors(current.Position))
            {
                // Skip if already visited or blocked
                if (closedSet.Contains(neighborPos) || IsBlocked(neighborPos))
                    continue;

                // Calculate new path cost
                float newG = current.G + 1;
                float newF = newG + Heuristic(neighborPos, endGrid);

                if (!allNodes.TryGetValue(neighborPos, out AStarNode neighborNode))
                {
                    // New node discovered
                    neighborNode = new AStarNode(neighborPos, newG, newF, current);
                    openSet.Enqueue(neighborNode, newF);
                    allNodes.Add(neighborPos, neighborNode);
                }
                else if (newG < neighborNode.G)
                {
                    // Found better path to existing node
                    neighborNode.G = newG;
                    neighborNode.F = newF;
                    neighborNode.Parent = current;
                    openSet.UpdatePriority(neighborNode, newF);
                }
            }
        }

        // No path found
        return new List<Vector2>();
    }

    private bool IsBlocked(Vector2IntR gridPos)
    {
        // Out of bounds is blocked
        if (!roomGenerator.IsWithinGrid(gridPos)) return true;

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
            return true; // Block if door not found
        }

        return false; // All other tiles are passable
    }

    private float Heuristic(Vector2IntR a, Vector2IntR b)
    {
        // Manhattan distance for grid-based pathfinding
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }

    private List<Vector2IntR> GetNeighbors(Vector2IntR pos)
    {
        return new List<Vector2IntR>
        {
            new Vector2IntR(pos.x - 1, pos.y), // Left
            new Vector2IntR(pos.x + 1, pos.y), // Right
            new Vector2IntR(pos.x, pos.y - 1), // Down
            new Vector2IntR(pos.x, pos.y + 1)  // Up
        };
    }

    private List<Vector2> ReconstructPath(AStarNode endNode)
    {
        List<Vector2> path = new List<Vector2>();
        AStarNode current = endNode;

        while (current != null)
        {
            // Convert grid position to world position (center of tile)
            path.Add(new Vector2(current.Position.x + 0.5f, current.Position.y + 0.5f));
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    private class AStarNode
    {
        public Vector2IntR Position { get; }
        public float G { get; set; } // Cost from start
        public float F { get; set; } // G + heuristic
        public AStarNode Parent { get; set; }

        public AStarNode(Vector2IntR position, float g, float f, AStarNode parent)
        {
            Position = position;
            G = g;
            F = f;
            Parent = parent;
        }
    }
}