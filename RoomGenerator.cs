using System.Collections.Generic;
using System;
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
    public int[,] intgrid { get; private set; }
    public List<Room> rooms { get; private set; } = new List<Room>();

    private HashSet<Vector2IntR> openSet;
    private HashSet<Vector2IntR> wallSet = new HashSet<Vector2IntR>();
    private HashSet<Vector2IntR> roomSet = new HashSet<Vector2IntR>();
    private HashSet<Vector2IntR> doorSet = new HashSet<Vector2IntR>();

    private HashSet<Vector2IntR> removedDoubleWalls;

    //TODO store room number on grid as int
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
        public HashSet<Vector2IntR> coords;
        public HashSet<Vector2IntR> edgeCoords;
        public HashSet<Vector2IntR> walls;
        public HashSet<Vector2IntR> doors;
        public Vector2IntR startCoord { get; private set; }
        public Color color { get; private set; }

        public Room(Vector2IntR startPosition)
        {
            walls = new HashSet<Vector2IntR>();
            doors = new HashSet<Vector2IntR>();
            coords = new HashSet<Vector2IntR> { startPosition };
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
            foreach (Vector2IntR offset in roomGenerator.GetOffsetDirections()) //TODO should be cardinal?
            {
                Vector2IntR roomCoord = coord + offset;
                if (!roomGenerator.IsWithinGrid(roomCoord)) return true;
                if (!coords.Contains(roomCoord)) return true;
            }

            return false;
        }
    }

    public class Door
    {
        public Vector2IntR position;
        public Room roomA;
        public Room roomB;
        public bool isOpen;

        public Door(Vector2IntR position, Room roomA, Room roomB, bool isOpen = false)
        {
            this.position = position;
            this.roomA = roomA;
            this.roomB = roomB;
            this.isOpen = isOpen;
        }
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
        //CopyRoomsToIntGrid();
    }

    private void InitializeGrid()
    {
        grid = new bool[gridWidth, gridHeight];
        intgrid = new int[gridWidth, gridHeight];
        openSet = new HashSet<Vector2IntR>(gridWidth * gridHeight);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                //is edge? -> generate a border wall
                if (x == 0 || y == 0 || x == gridWidth - 1 || y == gridHeight - 1)
                {
                    grid[x, y] = true;
                    intgrid[x, y] = 1;
                    wallSet.Add(new Vector2IntR(x, y));
                }
                else
                {
                    grid[x, y] = false;
                    intgrid[x, y] = 0;
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
            intgrid[coord.x, coord.y] = 0;
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
                intgrid[offsetedCoord.x, offsetedCoord.y] = 0;
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
                intgrid[wallCoord.x, wallCoord.y] = 1;
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

                    if (wallSet.Contains(singleOffset) && wallSet.Contains(doubleOffset)) //is double wall?
                    {
                        HashSet<Vector2IntR> wallNeighbours = new HashSet<Vector2IntR>();
                        HashSet<Room> roomNeighbours = new HashSet<Room>();

                        foreach (Vector2IntR singleDir in GetOffsetDirections())
                        {
                            Vector2IntR singleOffsetNeighbour = singleOffset + singleDir;

                            if (wallSet.Contains(singleOffsetNeighbour))
                            {
                                wallNeighbours.Add(singleOffsetNeighbour);
                            }

                            if (roomSet.Contains(singleOffsetNeighbour))
                            {
                                roomNeighbours.Add(CoordinateToRoomSlow(singleOffsetNeighbour));
                            }
                        }

                        bool isAttachable = roomNeighbours.Count == 1;

                        if (isAttachable)
                        {
                            room.walls.Remove(singleOffset);
                            foreach (Vector2IntR wall in wallNeighbours) room.walls.Add(wall);
                            room.coords.Add(singleOffset);
                            wallSet.Remove(singleOffset);
                            roomSet.Add(singleOffset);

                            removedDoubleWalls.Add(singleOffset);
                        }

                        break;
                    }
                }
            }
        }
    }

    private void MapCoordsToRooms()
    {
        coordToRoomMap = new Dictionary<Vector2IntR, Room>(roomSet.Count);
        int roomAreas = 0;

        foreach (Room room in rooms)
        {
            roomAreas += room.coords.Count;

            foreach (Vector2IntR coord in room.coords)
            {
                coordToRoomMap[coord] = room;
            }
        }
    }

    private void CopyRoomsToIntGrid()
    {
        foreach (Room room in rooms)
        {
            //foreach (Vector2IntR coord in room.coords) intgrid[coord.x, coord.y] = 0;
            //foreach (Vector2IntR coord in room.walls) intgrid[coord.x, coord.y] = 1;
            //foreach (Vector2IntR coord in room.doors) intgrid[coord.x, coord.y] = 2;
        }
    }

    private void GenerateDoors()
    {
        List<Door> doors = CollectDoors();
        ShuffleList(doors);

        UnionFind<Room> unionFind = new UnionFind<Room>(rooms);
        List<Door> selectedDoors = new List<Door>();

        // Minimum spanning tree for connectivity
        for (int i = 0; i < doors.Count; i++)
        {
            Door door = doors[i];
            if (!unionFind.AreConnected(door.roomA, door.roomB))
            {
                unionFind.Union(door.roomA, door.roomB);
                selectedDoors.Add(door);
                if (unionFind.GetNumberOfSets() == 1) break;
            }
        }

        // Random extra connections
        int remaining = doors.Count - selectedDoors.Count;
        int extraDoors = Random.Range(0f, 1f) < extraDoorChance ? 1 : 0; //TODO doors shouldn't be next to each other
        for (int i = 0; i < extraDoors; i++)
        {
            int randomIndex = Random.Range(selectedDoors.Count, doors.Count);
            selectedDoors.Add(doors[randomIndex]);
        }

        // Create door openings
        for (int i = 0; i < selectedDoors.Count; i++)
        {
            Vector2IntR doorPos = selectedDoors[i].position;
            doorSet.Add(doorPos);
            wallSet.Remove(doorPos);
            intgrid[doorPos.x, doorPos.y] = 2;

            for (int r = 0; r < rooms.Count; r++)
            {
                Room room = rooms[r];
                if (room.walls.Contains(doorPos))
                {
                    room.walls.Remove(doorPos);
                    room.doors.Add(doorPos);
                }
            }
        }
    }

    private List<Door> CollectDoors()
    {
        List<Door> doors = new List<Door>();
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
                        doors.Add(new Door(wall, adjacentRooms[i], adjacentRooms[j]));
                    }
                }

                processedWalls.Add(wall);
            }
        }
        return doors;
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

    public Room CoordinateToRoom(Vector2IntR coord)
    {
        coordToRoomMap.TryGetValue(coord, out Room room);
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

    public void PrintIntGrid()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Console.Write(intgrid[x, y] + " ");
            }
            Console.WriteLine();
        }
    }
}