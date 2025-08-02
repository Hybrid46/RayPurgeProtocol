using System.Numerics;
using Random = RandomR;
using Color = Raylib_cs.Color;

public class RoomGenerator
{
    public int gridWidth = 50;
    public int gridHeight = 50;

    public int minRoomSizeX = 2;
    public int maxRoomSizeX = 10;
    public int minRoomSizeY = 2;
    public int maxRoomSizeY = 10;
    //Range 0-1
    public float extraDoorChance = 0.0f;

    public bool[,] grid { get; private set; }
    public GridObject[,] objectGrid { get; private set; }
    public List<Room> rooms { get; private set; } = new List<Room>();

    private HashSet<Vector2IntR> openSet;
    private HashSet<Vector2IntR> wallSet = new HashSet<Vector2IntR>();
    private HashSet<Vector2IntR> roomSet = new HashSet<Vector2IntR>();
    private HashSet<Vector2IntR> doorSet = new HashSet<Vector2IntR>();
    private HashSet<Vector2IntR> removedDoubleWalls;

    private Dictionary<Vector2IntR, Room> coordToRoomMap;

    public RoomGenerator(int gridWidth, int gridHeight, int minRoomSizeX, int maxRoomSizeX, int minRoomSizeY, int maxRoomSizeY, float extraDoorChance)
    {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.minRoomSizeX = minRoomSizeX;
        this.maxRoomSizeX = maxRoomSizeX;
        this.minRoomSizeY = minRoomSizeY;
        this.maxRoomSizeY = maxRoomSizeY;
        this.extraDoorChance = extraDoorChance;
    }

    public class Room
    {
        public HashSet<Vector2IntR> coords;     // Empty space
        public HashSet<Vector2IntR> edgeCoords; // Edge of the map -> must be in walls too
        public HashSet<Vector2IntR> walls;      // Walls around the room
        public HashSet<Door> doors;             // Doors in the room
        public Vector2IntR startCoord { get; private set; } //Room center, where the room was generated from
        public Color color { get; private set; }
        public HashSet<Room> neighbourRooms;

        public Room(Vector2IntR startPosition)
        {
            walls = new HashSet<Vector2IntR>();
            doors = new HashSet<Door>();
            coords = new HashSet<Vector2IntR> { startPosition };
            neighbourRooms = new HashSet<Room>();
            this.startCoord = startPosition;
            color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 0.4f);
        }

        public void SetEdges(RoomGenerator roomGenerator)
        {
            edgeCoords = new HashSet<Vector2IntR>();

            foreach (Vector2IntR coord in coords)
            {
                if (IsEdge(roomGenerator, coord)) edgeCoords.Add(coord);
            }
        }

        private bool IsEdge(RoomGenerator roomGenerator, Vector2IntR coord)
        {
            foreach (Vector2IntR offset in roomGenerator.GetOffsetDirections())
            {
                Vector2IntR roomCoord = coord + offset;
                if (!roomGenerator.IsWithinGrid(roomCoord) || !coords.Contains(roomCoord)) return true;
            }
            return false;
        }

        public Door CoordinateToDoor(Vector2IntR coord)
        {
            foreach (Door door in doors)
            {
                if (door.position == coord)
                {
                    return door;
                }
            }

            return null;
        }
    }

    public abstract class GridObject
    {
        public abstract int Type { get; }
        public abstract Color minimapColor { get; }
        public abstract string textureName { get; }
    }

    public class Wall : GridObject
    {
        public override int Type => 1;
        public override Color minimapColor => new Color(100, 100, 100, 255);
        public override string textureName => "wall";
    }

    public class Door : GridObject
    {
        public Vector2IntR position;
        public Room roomA;
        public Room roomB;
        public bool isOpen;
        public override int Type => 2;
        public override Color minimapColor => isOpen ? new Color(0, 0, 150, 255) : new Color(0, 0, 255, 255);
        public override string textureName => "door";

        public Door(Vector2IntR position, Room roomA, Room roomB, bool isOpen = false)
        {
            this.position = position;
            this.roomA = roomA;
            this.roomB = roomB;
            this.isOpen = isOpen;
        }
    }

    public class Floor : GridObject
    {
        public override int Type => 0;
        public override Color minimapColor => new Color(30, 30, 30, 255);
        public override string textureName => "floor";
    }

    public void Generate()
    {
        InitializeGrid();
        GenerateRooms();

        foreach (Room room in rooms) room.SetEdges(this);

        //removing doulbe walls
        AttachDoubleWallsToRooms();
        foreach (Room room in rooms) room.SetEdges(this);

        //Doors
        GenerateDoors();

        MapCoordsToRooms();
    }

    private void InitializeGrid()
    {
        grid = new bool[gridWidth, gridHeight];
        objectGrid = new GridObject[gridWidth, gridHeight];
        openSet = new HashSet<Vector2IntR>(gridWidth * gridHeight);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                //is edge? -> generate a border wall
                if (x == 0 || y == 0 || x == gridWidth - 1 || y == gridHeight - 1)
                {
                    grid[x, y] = true;
                    objectGrid[x, y] = new Wall();
                    wallSet.Add(new Vector2IntR(x, y));
                }
                else
                {
                    grid[x, y] = false;
                    objectGrid[x, y] = new Floor();
                    openSet.Add(new Vector2IntR(x, y));
                }
            }
        }
    }

    private void GenerateRooms()
    {
        rooms = new List<Room>();

        while (openSet.Count > 0)
        {
            Vector2IntR coord = GetFirstElementFromHashSet(openSet);
            int width = Random.Range(minRoomSizeX, maxRoomSizeX + 1);
            int height = Random.Range(minRoomSizeY, maxRoomSizeY + 1);

            Console.WriteLine($"Generating room at {coord} with size {width}x{height}");

            Room room = new Room(coord);

            roomSet.Add(coord);
            rooms.Add(room);
            grid[coord.x, coord.y] = true;
            objectGrid[coord.x, coord.y] = new Floor();
            openSet.Remove(coord);

            ExpandRoom(coord, width, height, room);
            GenerateWallsAroundRoom(room);
        }
    }

    private void ExpandRoom(Vector2IntR coord, int width, int height, Room room)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2IntR offsetedCoord = coord + new Vector2IntR(x, y);

                //bounds check
                if (!IsWithinGrid(offsetedCoord)) continue;
                //occupancy check
                if (grid[offsetedCoord.x, offsetedCoord.y]) continue;

                room.coords.Add(offsetedCoord);
                roomSet.Add(offsetedCoord);
                grid[offsetedCoord.x, offsetedCoord.y] = true;
                objectGrid[offsetedCoord.x, offsetedCoord.y] = new Floor();
                openSet.Remove(offsetedCoord);
            }
        }
    }

    private void GenerateWallsAroundRoom(Room room)
    {
        List<Vector2IntR> offsetDirections = GetOffsetDirections();

        foreach (Vector2IntR roomCoord in room.coords)
        {
            foreach (Vector2IntR offsetDirection in offsetDirections)
            {
                Vector2IntR wallCoord = roomCoord + offsetDirection;

                //bounds check
                if (!IsWithinGrid(wallCoord)) continue;
                //occupancy check
                if (grid[wallCoord.x, wallCoord.y]) continue;

                room.walls.Add(wallCoord);
                wallSet.Add(wallCoord);
                grid[wallCoord.x, wallCoord.y] = true;
                objectGrid[wallCoord.x, wallCoord.y] = new Wall();
                openSet.Remove(wallCoord);
            }
        }
    }

    private void AttachDoubleWallsToRooms()
    {
        removedDoubleWalls = new HashSet<Vector2IntR>();

        foreach (Room room in rooms)
        {
            foreach (Vector2IntR edge in room.edgeCoords)
            {
                foreach (Vector2IntR dir in GetCardinalDirections())
                {
                    Vector2IntR singleOffset = edge + dir;
                    Vector2IntR doubleOffset = edge + (dir * 2);

                    // Boundary checks
                    if (!IsWithinGrid(singleOffset)) continue;
                    if (!IsWithinGrid(doubleOffset)) continue;

                    if (wallSet.Contains(singleOffset) && wallSet.Contains(doubleOffset)) // Double wall detected
                    {
                        HashSet<Vector2IntR> wallNeighbours = new HashSet<Vector2IntR>();
                        HashSet<Room> roomNeighbours = new HashSet<Room>();

                        // Check neighbors of the inner wall (singleOffset)
                        foreach (Vector2IntR singleDir in GetOffsetDirections())
                        {
                            Vector2IntR neighbor = singleOffset + singleDir;
                            if (!IsWithinGrid(neighbor)) continue;

                            if (wallSet.Contains(neighbor))
                                wallNeighbours.Add(neighbor);

                            if (roomSet.Contains(neighbor))
                                roomNeighbours.Add(CoordinateToRoomSlow(neighbor));
                        }

                        // Proceed only if adjacent to one room (current room)
                        if (roomNeighbours.Count == 1)
                        {
                            // Update grid: convert wall to floor
                            grid[singleOffset.x, singleOffset.y] = false;
                            objectGrid[singleOffset.x, singleOffset.y] = new Floor();

                            // Update room structure
                            room.walls.Remove(singleOffset);
                            foreach (Vector2IntR wall in wallNeighbours)
                                room.walls.Add(wall);
                            room.coords.Add(singleOffset);

                            // Update global sets
                            wallSet.Remove(singleOffset);
                            roomSet.Add(singleOffset);
                            removedDoubleWalls.Add(singleOffset);
                        }
                    }
                }
            }
        }
    }

    private void MapCoordsToRooms()
    {
        coordToRoomMap = new Dictionary<Vector2IntR, Room>(roomSet.Count);

        foreach (Room room in rooms)
        {
            foreach (Vector2IntR coord in room.coords)
            {
                coordToRoomMap[coord] = room;
            }

            foreach (Vector2IntR coord in room.walls)
            {
                coordToRoomMap[coord] = room;
            }

            foreach (Door door in room.doors)
            {
                coordToRoomMap[door.position] = room;
            }
        }
    }

    private void GenerateDoors()
    {
        List<Door> doors = CollectDoors();
        ShuffleList(doors);

        UnionFind<Room> unionFind = new UnionFind<Room>(rooms);
        List<Door> selectedDoors = new List<Door>();

        // Build minimum spanning tree to ensure connectivity
        foreach (Door door in doors)
        {
            if (!unionFind.AreConnected(door.roomA, door.roomB))
            {
                unionFind.Union(door.roomA, door.roomB);
                selectedDoors.Add(door);
                if (unionFind.GetNumberOfSets() == 1) break;
            }
        }

        // Potential extra random connections
        if (Random.Range(0f, 1f) < extraDoorChance)
        {
            // pick one additional door
            Door extra = doors[Random.Range(selectedDoors.Count, doors.Count)];
            selectedDoors.Add(extra);
        }

        // Carve out doors and record them
        foreach (Door d in selectedDoors)
        {
            Vector2IntR doorPos = d.position;
            doorSet.Add(doorPos);
            wallSet.Remove(doorPos);

            // Remove wall and add door to each room
            d.roomA.walls.Remove(doorPos);
            d.roomB.walls.Remove(doorPos);
            Door newDoor = new Door(doorPos, d.roomA, d.roomB, false);
            objectGrid[doorPos.x, doorPos.y] = newDoor;
            d.roomA.doors.Add(newDoor);
            d.roomB.doors.Add(newDoor);

            // Link neighbours directly
            d.roomA.neighbourRooms.Add(d.roomB);
            d.roomB.neighbourRooms.Add(d.roomA);
        }
    }

    private List<Door> CollectDoors()
    {
        HashSet<Door> doors = new HashSet<Door>();
        HashSet<Vector2IntR> processedWalls = new HashSet<Vector2IntR>();

        foreach (Vector2IntR wall in wallSet)
        {
            if (processedWalls.Contains(wall)) continue;

            List<Room> adjacentRooms = new List<Room>();
            foreach (Vector2IntR dir in GetCardinalDirections())
            {
                Vector2IntR neighbor = wall + dir;
                Room room = CoordinateToRoomSlow(neighbor);

                if (room != null && !adjacentRooms.Contains(room))
                {
                    adjacentRooms.Add(room);
                }
            }

            if (adjacentRooms.Count >= 2)
            {
                for (int i = 0; i < adjacentRooms.Count; i++)
                {
                    for (int j = i + 1; j < adjacentRooms.Count; j++)
                    {
                        if (!doorSet.Contains(wall))
                        {
                            doors.Add(new Door(wall, adjacentRooms[i], adjacentRooms[j], false));
                        }
                        else
                        {
                            // If door already exists, just link the rooms
                            Door existingDoor = (Door)objectGrid[wall.x, wall.y];
                            existingDoor.roomA = adjacentRooms[i];
                            existingDoor.roomB = adjacentRooms[j];
                        }
                    }
                }

                processedWalls.Add(wall);
            }
        }
        return new List<Door>(doors);
    }

    // Union-Find implementation
    public class UnionFind<T>
    {
        private Dictionary<T, T> parent;
        private Dictionary<T, int> rank;
        private int numSets;

        public UnionFind(IEnumerable<T> elements)
        {
            parent = new Dictionary<T, T>();
            rank = new Dictionary<T, int>();
            numSets = 0;

            foreach (T element in elements) MakeSet(element);
        }

        public void MakeSet(T element)
        {
            if (!parent.ContainsKey(element))
            {
                parent[element] = element;
                rank[element] = 0;
                numSets++;
            }
        }

        public T Find(T element)
        {
            if (!parent[element].Equals(element))
            {
                parent[element] = Find(parent[element]);
            }

            return parent[element];
        }

        public void Union(T a, T b)
        {
            T rootA = Find(a);
            T rootB = Find(b);

            if (rootA.Equals(rootB)) return;

            if (rank[rootA] < rank[rootB])
            {
                parent[rootA] = rootB;
            }
            else
            {
                parent[rootB] = rootA;
                if (rank[rootA] == rank[rootB]) rank[rootA]++;
            }
            numSets--;
        }

        public bool AreConnected(T a, T b) => Find(a).Equals(Find(b));
        public int GetNumberOfSets() => numSets;
    }

    //Helper methods

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n; i++)
        {
            int r = i + Random.Range(0, n - i);
            T value = list[r];
            list[r] = list[i];
            list[i] = value;
        }
    }

    private List<Vector2IntR> GetOffsetDirections()
    {
        List<Vector2IntR> offsetDirections = new List<Vector2IntR>(8);

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                offsetDirections.Add(new Vector2IntR(x, y));
            }
        }

        return offsetDirections;
    }

    private List<Vector2IntR> GetCardinalDirections()
    {
        List<Vector2IntR> offsetDirections = new List<Vector2IntR>(4)
        {
            new Vector2IntR(-1, 0),
            new Vector2IntR(1, 0),
            new Vector2IntR(0, 1),
            new Vector2IntR(0, -1)
        };

        return offsetDirections;
    }

    public bool IsWithinGrid(Vector2IntR pos) => pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;

    public bool IsGridEdge(Vector2IntR pos) => pos.x == 0 || pos.x == gridWidth - 1 || pos.y == 0 || pos.y == gridHeight - 1;

    public bool IsWalkable(Vector2 position)
    {
        Vector2IntR gridPos = new Vector2IntR(position);
        return IsWalkable(gridPos);
    }

    public bool IsWalkable(Vector2IntR gridPos)
    {
        if (objectGrid[gridPos.x, gridPos.y] is Floor) return true;
        if (objectGrid[gridPos.x, gridPos.y] is Door door) return door.isOpen;

        return false;
    }

    public Room GetRoomAtPosition(Vector2 position)
    {
        Vector2IntR gridPos = new Vector2IntR(position);
        return GetRoomAtPosition(gridPos);
    }

    public Room GetRoomAtPosition(Vector2IntR gridPos) {
        coordToRoomMap.TryGetValue(gridPos, out Room room);
        return room;
    }

    private Room CoordinateToRoomSlow(Vector2IntR coord)
    {
        foreach (Room room in rooms)
        {
            if (room.coords.Contains(coord)) return room;
        }

        return null;
    }

    private T GetFirstElementFromHashSet<T>(HashSet<T> hashSet)
    {
        foreach (T t in hashSet)
        {
            return t;
        }

        return default;
    }

    public void PrintRoom(Room room) => PrintRoom(rooms.IndexOf(room));

    public void PrintRoom(int index)
    {
        Room room = rooms[index];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                bool isDoor = false;

                foreach (Door door in room.doors)
                {
                    if (door.position.x == x && door.position.y == y)
                    {
                        isDoor = true;
                        break;
                    }
                }

                Vector2IntR coord = new Vector2IntR(x, y);
                if (room.coords.Contains(coord)) Console.Write("O");
                else if (room.walls.Contains(coord)) Console.Write("W");
                else if (room.edgeCoords.Contains(coord)) Console.Write("E");
                else if (isDoor) Console.Write("D");
                else Console.Write("_");
            }
            Console.WriteLine();
        }

        foreach (Room neighbour in room.neighbourRooms)
        {
            Console.WriteLine($"Room {index} has neighbour: {rooms.IndexOf(neighbour)} in: {neighbour.startCoord}");
        }

        //foreach (Vector2IntR coord in room.coords)
        //{
        //    Console.WriteLine($"Room {index} contains coord: {coord}");
        //}

        //foreach (Vector2IntR wall in room.walls)
        //{
        //    Console.WriteLine($"Room {index} has wall at: {wall}");
        //}

        //foreach (Vector2IntR door in room.doors)
        //{
        //    Console.WriteLine($"Room {index} has door at: {door}");
        //}

        //foreach (Vector2IntR edge in room.edgeCoords)
        //{
        //    Console.WriteLine($"Room {index} has edge at: {edge}");
        //}
    }
}