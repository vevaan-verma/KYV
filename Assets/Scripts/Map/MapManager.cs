using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour {

    [Header("References")]
    [SerializeField] private Tilemap borderTilemap; // used as the reference tilemap for positioning because it encloses the room
    [SerializeField] private Tilemap interiorTilemap;
    [SerializeField] private Tilemap propTilemap;
    [SerializeField] private MapTile[] borderTiles;
    [SerializeField] private MapTile[] floorTiles;
    [SerializeField] private GameObject wallTilePrefab; // used to instantiate wall tiles for the border of the room (used for collisions)
    private RoundManager roundManager;
    private LoadingManager loadingManager;
    private AstarPath astarPath;

    [Header("Settings")]
    [SerializeField] private int roundRoomSizeIncrement;

    [Header("Room Generation")]
    /* 
     * EXPLANATION:
     * UNIQUE REQUIRED PROPS:
     * These are a type of required prop that have special placements or conditions that don't enable them to be placed with the other required props.
     * They are placed first and are not included in the requiredProps array.
     * 
     * REQUIRED PROPS:
     * Prop tile order is randomized and the prop tiles are iterated through in that order.
     * On each valid prop tile, if rotations are enabled, random ninety degree rotations are selected for the prop until a valid one is found.
     * If not, the zero degree rotation is used.
     * Once a valid rotation is found, the prop is placed on the prop tile.
     * This is repeated for each required prop and its quantity.
     * Essentially, there are two/three levels of probability: the iteration order of the tiles, the randomized order of props, and the rotation of the prop (if enabled).
     * 
     * OPTIONAL PROPS:
     * On each valid prop tile, propSpawnProbability is the probability that a prop will be spawned on that tile.
     * If a prop can be spawned, the order of the optionalProps array is randomized.
     * For each prop in the randomized array, the probability of its spawn is checked.
     * If the prop can be spawned, the prop is placed on the prop tile.
     * If not, the next prop is checked.
     * When placing the prop, if rotations are enabled, random ninety degree rotations are selected for the prop until a valid one is found.
     * If not, the zero degree rotation is used.
     * Essentially, there are four/five levels of probability: the iteration order of the tiles, the probability that a prop will spawn on a tile, the randomized order of props, the probability that a specific prop will be spawned, and the rotation of the prop (if enabled).
     * 
     * Prior to placing a prop, a random variation of the prop is selected.
     * Therefore, the total probability of the props spawning must be equal to one hundred.
     * 
     * Props are spawned from the bottom to the top and left to right.
     * Margins are always respective to the prop's zero degree rotation.
    */
    [SerializeField][Min(4)] private int baseRoomWidth; // must be greater than or equal to four to allow for proper room generation
    [SerializeField][Min(4)] private int baseRoomHeight; // must be greater than or equal to four to allow for proper room generation
    private int roomWidth;
    private int roomHeight;

    [Header("Room Expansion")]
    [SerializeField] private bool expansionsEnabled;
    [SerializeField] private int expansionCount;
    [SerializeField][Tooltip("Must be less than or equal to the full base room width plus one")][Min(4)] private int minExpansionWidth; // must be greater than or equal to four to allow for proper room generation
    [SerializeField][Tooltip("Must be less than or equal to the full base room height plus one")][Min(4)] private int minExpansionHeight; // must be greater than or equal to four to allow for proper room generation
    private bool[,] interiorTiles;

    [Header("Props")]
    [SerializeField] private Prop lunchboxProp; // required prop that will always spawn in the center of the base room (so it is not included in the requiredProps array)
    [Space]
    [SerializeField] private RequiredProp[] requiredProps; // required props; will always spawn
    [Space]
    [SerializeField][Tooltip("The probability that a prop will spawn on the current tile")][Range(0f, 100f)] private float propSpawnProbability;
    [SerializeField] private OptionalProp[] optionalProps;
    private readonly List<RotationType> rotations = new List<RotationType> { RotationType.ROTATE_0, RotationType.ROTATE_90, RotationType.ROTATE_180, RotationType.ROTATE_270 };
    private bool[,] marginedInteriorTiles; // this also comes into play after interior tiles are filled but before props are placed; it tracks which interior tiles are still open for prop placement (accounts for margins; occupied and margin tiles are marked as false)
    private bool[,] occupiedInteriorTiles; // this comes into play after interior tiles are filled but before props are placed; it tracks the tiles that are still open for spawning (ignores margins; occupied tiles are marked as false)

    [Header("Spawn")]
    private Vector3 playerGridSpawn;
    private Vector3 playerWorldSpawn;

    // do this on awake to ensure that the room is generated before the player is spawned
    private void Awake() {

        roundManager = FindFirstObjectByType<RoundManager>();
        loadingManager = FindFirstObjectByType<LoadingManager>();

        // apply round room size multiplier
        roomWidth = baseRoomWidth + ((GameData.GetRoundNumber() - 1) * roundRoomSizeIncrement);
        roomHeight = baseRoomHeight + ((GameData.GetRoundNumber() - 1) * roundRoomSizeIncrement);

        #region VALIDATION
        if (!(borderTilemap.transform.position.Equals(interiorTilemap.transform.position) && interiorTilemap.transform.position.Equals(propTilemap.transform.position)))
            Debug.LogWarning("Tilemap positions are not equal. This may result in misaligned tiles.");

        if (!(borderTilemap.cellSize.Equals(interiorTilemap.cellSize) && interiorTilemap.cellSize.Equals(propTilemap.cellSize)))
            Debug.LogWarning("Tilemap cell sizes are not equal. This may result in misaligned tiles.");

        bool expansionError = false;

        if (expansionsEnabled) { // check if expansions are enabled (errors are only relevant if expansions are enabled)

            if (roomWidth - minExpansionWidth + 3 < 0) {

                Debug.LogError("Minimum expansion width is too large. This will result in expansions that do not fit in the constraints of the room.\nMinimum expansion width must be less than or equal to the base room width plus one.");

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
Application.Quit();
#endif

                expansionError = true;

            }

            if (roomHeight - minExpansionHeight + 3 < 0) {

                Debug.LogError("Minimum expansion height too large. This will result in expansions that do not fit in the constraints of the room.\nMinimum expansion height must be less than or equal to the base room height plus one.");

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
Application.Quit();
#endif

                expansionError = true;

            }
        }

        if (expansionError) // check if there is an expansion error
            return; // return to prevent further errors

        // make sure that the total probability of the floor tiles is equal to 100
        float totalProbability = 0f;

        foreach (MapTile floorTile in floorTiles)
            totalProbability += floorTile.GetSpawnProbability();

        if (totalProbability != 100f)
            Debug.LogWarning("Total floor tile probability is not equal to 100. This may result in blank tiles in the room or inaccuracies in tile placements.");

        // make sure that the total probability of the props is equal to 100
        totalProbability = 0f;

        foreach (OptionalProp optionalProp in optionalProps)
            totalProbability += optionalProp.GetSpawnProbability();

        if (totalProbability != 100f)
            Debug.LogWarning("Total prop probability is not equal to 100. This may result in a lack of props or inaccuracies in prop placements.");

        #endregion

        astarPath = GetComponent<AstarPath>();
        interiorTiles = new bool[roomWidth * 3, roomHeight * 3]; // initialize interior tiles (accounts for maximum room size including expansions)

        bool[,] roomLayout = GenerateFilledMap(); // generate filled map with base room and expansions
        ClearMapInterior(roomLayout); // remove interior of room
        FillInteriorTiles(); // fill in interior tiles with other tiles
        SpawnRequiredProps(); // spawn required props
        SpawnOptionalProps(); // spawn optional props
        GeneratePlayerSpawn(); // generate spawn point locations

    }

    private IEnumerator Start() {

        yield return null; // wait for one frame to ensure that the room is generated before the graph is scanned

        // change recast graph shape
        RecastGraph recastGraph = astarPath.data.recastGraph;
        recastGraph.forcedBoundsCenter = new Vector3((roomWidth * 3f) / 2f, (roomHeight * 3f) / 2f, 0f);
        recastGraph.forcedBoundsSize = new Vector3(roomWidth * 3f, 1f, roomHeight * 3f);
        recastGraph.Scan();

        // wait for the graph to be scanned
        while (!recastGraph.isScanned)
            yield return null;

        loadingManager.HideLoadingScreen(); // hide the loading screen after the graph is done scanning (IMPORTANT: only applies to round scenes, not the intermission scene)
        roundManager.StartRound(); // start the round

    }

    // generates a filled map with a base room and expansions; returns the room layout
    private bool[,] GenerateFilledMap() {

        bool[,] roomLayout = new bool[roomWidth * 3, roomHeight * 3]; // initialize room layout (accounts for maximum room size including expansions)

        // generate initial room rectangle
        for (int x = roomWidth; x < roomWidth * 2; x++) { // iterate through x values of room layout

            for (int y = roomHeight; y < roomHeight * 2; y++) { // iterate through y values of room layout

                float probability = Random.Range(0f, 100f); // random probability to select a border tile based on its spawn probability

                float cumulativeProbability = 0f; // used to track the cumulative probability of the border tiles

                // find which tile the random number falls onto
                foreach (MapTile borderTile in borderTiles) {

                    cumulativeProbability += borderTile.GetSpawnProbability(); // track the cumulative probability of the border tiles

                    if (probability <= cumulativeProbability) { // check if random number falls onto current tile

                        borderTilemap.SetTile(new Vector3Int(x, y, 0), borderTile.GetTile()); // fill in interior tile with random floor tile
                        roomLayout[x, y] = true; // mark as part of room
                        break; // required or else the last border tile will always be placed

                    }
                }
            }
        }

        if (expansionsEnabled) { // check if expansions are enabled

            for (int i = 0; i < expansionCount; i++) {

                // +1 on the upper bound to include the endpoint of the interval (random range is exclusive on upper bound)
                int width = Random.Range(minExpansionWidth, roomWidth + 1);
                int height = Random.Range(minExpansionHeight, roomHeight + 1);
                int xStart = Random.Range(roomWidth - width + 3, roomWidth * 2 - 2); // minimum and maximum x values ensure that the expansion is always connected to the room body (has at least one tile in common)
                int yStart = Random.Range(roomHeight - height + 3, roomHeight * 2 - 2); // minimum and maximum y values ensure that the expansion is always connected to the room body (has at least one tile in common)

                for (int x = xStart; x < xStart + width; x++) { // iterate through x values of room layout

                    for (int y = yStart; y < yStart + height; y++) { // iterate through y values of room layout

                        if (borderTilemap.GetTile(new Vector3Int(x, y, 0)) == null) { // check if tile is empty

                            float probability = Random.Range(0f, 100f); // random probability to select a border tile based on its spawn probability

                            float cumulativeProbability = 0f; // used to track the cumulative probability of the border tiles

                            // find which tile the random number falls onto
                            foreach (MapTile borderTile in borderTiles) {

                                cumulativeProbability += borderTile.GetSpawnProbability(); // track the cumulative probability of the border tiles

                                if (probability <= cumulativeProbability) { // check if random number falls onto current tile

                                    borderTilemap.SetTile(new Vector3Int(x, y, 0), borderTile.GetTile()); // fill in interior tile with random floor tile
                                    roomLayout[x, y] = true; // mark as part of room
                                    break; // required or else the last border tile will always be placed

                                }
                            }
                        }
                    }
                }
            }
        }

        return roomLayout;

    }

    // removes the interior of the room by checking if a tile is surrounded by other tiles on all sides including diagonals or is on the edge of the room
    private void ClearMapInterior(bool[,] roomLayout) {

        // check if tile is surrounded by other tiles on all sides including diagonals or is on the edge of the room
        for (int x = 0; x < roomLayout.GetLength(0); x++) { // iterate through x values of room layout

            for (int y = 0; y < roomLayout.GetLength(1); y++) { // iterate through y values of room layout

                if (roomLayout[x, y]) { // check if tile is part of room

                    if (x == 0 || x == roomLayout.GetLength(0) - 1 || y == 0 || y == roomLayout.GetLength(1) - 1) // check if tile is on the edge of the room
                        continue; // skip tile because it is a border tile

                    if (roomLayout[x - 1, y] && roomLayout[x + 1, y] && roomLayout[x, y - 1] && roomLayout[x, y + 1] && roomLayout[x - 1, y - 1] && roomLayout[x + 1, y - 1] && roomLayout[x - 1, y + 1] && roomLayout[x + 1, y + 1]) { // check if tile is surrounded by other tiles on all sides including diagonals, meaning it is an interior tile

                        borderTilemap.SetTile(new Vector3Int(x, y, 0), null); // remove tile

                        // don't unflag as part of room since this will prevent adjacent tiles from being removed if they are also surrounded
                        interiorTiles[x, y] = true; // instead, mark as an interior tile to fill in with other tiles later

                    } else { // tile is not surrounded by other tiles on all sides including diagonals, meaning it is a border tile

                        GameObject wallTile = Instantiate(wallTilePrefab, borderTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0)), Quaternion.identity, borderTilemap.transform); // instantiate wall tile at the center of the tile position

                    }
                }
            }
        }
    }

    // fills in interior tiles with other tiles
    private void FillInteriorTiles() {

        for (int x = 0; x < interiorTiles.GetLength(0); x++) { // iterate through x values of interior tiles

            for (int y = 0; y < interiorTiles.GetLength(1); y++) { // iterate through y values of interior tiles

                if (interiorTiles[x, y]) { // check if tile is an interior tile

                    float probability = Random.Range(0f, 100f); // random probability to select a floor tile based on its spawn probability

                    float cumulativeProbability = 0f; // used to track the cumulative probability of the floor tiles

                    // find which tile the probability falls onto
                    foreach (MapTile floorTile in floorTiles) {

                        cumulativeProbability += floorTile.GetSpawnProbability(); // track the cumulative probability of the floor tiles

                        if (probability <= cumulativeProbability) { // check if random number falls onto current tile

                            interiorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile.GetTile()); // fill in interior tile with random floor tile
                            break; // required or else the last floor tile will always be placed

                        }
                    }
                }
            }
        }
    }

    private void SpawnRequiredProps() {

        occupiedInteriorTiles = interiorTiles.Clone() as bool[,]; // initialize open interior tiles with current interior tiles | accounts for just the prop placement locations (occupied tiles only)
        marginedInteriorTiles = interiorTiles.Clone() as bool[,]; // initialize buffered interior tiles with current interior tiles (to be used for prop placement) | accounts for the prop placement locations and their margins

        // add lunchbox prop tile in the center of the base room and make sure it is placeable
        if (!TryPlaceProp(lunchboxProp, 100f, new Vector2Int(lunchboxProp.GetWidth(), lunchboxProp.GetHeight()), new PropRotation(RotationType.ROTATE_0), (roomWidth * 3) / 2, (roomHeight * 3) / 2, lunchboxProp.GetTopMargin(), lunchboxProp.GetBottomMargin(), lunchboxProp.GetLeftMargin(), lunchboxProp.GetRightMargin())) // check if prop is not usable
            Debug.LogError("Lunchbox prop is not placeable. Margins need to be changed on the prop or the map needs to be regenerated."); // output error message

        List<Vector2Int> tileCoordinates = new List<Vector2Int>();

        for (int x = 0; x < occupiedInteriorTiles.GetLength(0); x++) // iterate through x values of prop tiles
            for (int y = 0; y < occupiedInteriorTiles.GetLength(1); y++) // iterate through y values of prop tiles
                if (occupiedInteriorTiles[x, y]) // check if tile is a prop tile
                    tileCoordinates.Add(new Vector2Int(x, y)); // add prop tile to list

        // randomize tile coordinates order
        tileCoordinates = tileCoordinates.OrderBy(x => Random.value).ToList();

        // placed up here because the props are removed from the list as they are placed, so a new list shouldn't be created each time a prop is placed
        List<RequiredProp> randomizedProps = new List<RequiredProp>();

        // add required props to list based on their amount
        foreach (RequiredProp requiredProp in requiredProps)
            for (int i = 0; i < requiredProp.GetAmount(); i++)
                randomizedProps.Add(requiredProp);

        // randomize prop order
        randomizedProps = randomizedProps.OrderBy(x => Random.value).ToList();

        foreach (Vector2Int tileCoordinate in tileCoordinates) { // iterate through randomized prop tiles

            int x = tileCoordinate.x;
            int y = tileCoordinate.y;

            if (occupiedInteriorTiles[x, y]) { // check if tile is a prop tile

                foreach (RequiredProp requiredProp in randomizedProps) {

                    Prop prop = requiredProp.GetProp();
                    bool propPlaced = true;

                    if (requiredProp.IsRotationEnabled()) {

                        List<PropRotation> randomizedRotations = new List<PropRotation>();

                        // randomize rotation order
                        randomizedRotations = rotations.Select(r => new PropRotation(r)).OrderBy(x => Random.value).ToList();

                        foreach (PropRotation propRotation in randomizedRotations) { // iterate through randomized prop rotations

                            propPlaced = TryPlaceProp(prop, 100f, new Vector2Int(prop.GetWidth(), prop.GetHeight()), propRotation, x, y, prop.GetTopMargin(), prop.GetBottomMargin(), prop.GetLeftMargin(), prop.GetRightMargin());

                            if (propPlaced) { // check if prop was placed

                                randomizedProps.Remove(requiredProp); // remove prop from list
                                break; // break out of rotation loop since prop is placed

                            }
                        }
                    } else {

                        propPlaced = TryPlaceProp(prop, 100f, new Vector2Int(prop.GetWidth(), prop.GetHeight()), new PropRotation(RotationType.ROTATE_0), x, y, prop.GetTopMargin(), prop.GetBottomMargin(), prop.GetLeftMargin(), prop.GetRightMargin());

                        if (propPlaced) // check if prop was placed
                            randomizedProps.Remove(requiredProp); // remove prop from list

                    }

                    if (propPlaced) // if prop was placed with any rotation
                        break; // break out of prop loop since prop is placed

                }
            }
        }

        // make sure all required props were placed and output an error if not
        if (randomizedProps.Count > 0)
            Debug.LogError("Not all required props could be placed. Please check the map configuration or prop settings. Consider changing the margins of the props if needed.");

    }

    // spawns optional, probability based props on prop tiles
    private void SpawnOptionalProps() {

        List<Vector2Int> tileCoordinates = new List<Vector2Int>();

        for (int x = 0; x < occupiedInteriorTiles.GetLength(0); x++) // iterate through x values of prop tiles
            for (int y = 0; y < occupiedInteriorTiles.GetLength(1); y++) // iterate through y values of prop tiles
                if (occupiedInteriorTiles[x, y]) // check if tile is a prop tile
                    tileCoordinates.Add(new Vector2Int(x, y)); // add prop tile to list

        // randomize tile coordinates order
        tileCoordinates = tileCoordinates.OrderBy(x => Random.value).ToList();

        foreach (Vector2Int tileCoordinate in tileCoordinates) { // iterate through randomized prop tiles

            int x = tileCoordinate.x;
            int y = tileCoordinate.y;

            if (occupiedInteriorTiles[x, y]) { // check if tile is a prop tile

                float tileSpawnProbability = Random.Range(0f, 100f); // probability of a prop spawning on this tile

                if (tileSpawnProbability <= propSpawnProbability) { // check if prop should be spawned

                    List<OptionalProp> randomizedProps = optionalProps.OrderBy(x => Random.value).ToList(); // randomize the order of the optional props to ensure variety in prop types/placements

                    foreach (OptionalProp optionalProp in randomizedProps) {

                        Prop prop = optionalProp.GetProp();
                        bool propPlaced = true;

                        if (optionalProp.IsRotationEnabled()) {

                            List<PropRotation> randomizedRotations = new List<PropRotation>();

                            // randomize rotation order
                            randomizedRotations = rotations.Select(r => new PropRotation(r)).OrderBy(x => Random.value).ToList();

                            foreach (PropRotation propRotation in randomizedRotations) { // iterate through randomized prop rotations

                                propPlaced = TryPlaceProp(prop, optionalProp.GetSpawnProbability(), new Vector2Int(prop.GetWidth(), prop.GetHeight()), propRotation, x, y, prop.GetTopMargin(), prop.GetBottomMargin(), prop.GetLeftMargin(), prop.GetRightMargin());

                                if (propPlaced) // check if prop was placed
                                    break; // break out of rotation loop since prop is placed

                            }
                        } else {

                            propPlaced = TryPlaceProp(prop, optionalProp.GetSpawnProbability(), new Vector2Int(prop.GetWidth(), prop.GetHeight()), new PropRotation(RotationType.ROTATE_0), x, y, prop.GetTopMargin(), prop.GetBottomMargin(), prop.GetLeftMargin(), prop.GetRightMargin());

                        }

                        if (propPlaced) // if prop was placed with any rotation
                            break; // break out of prop loop since prop is placed

                    }
                }
            }
        }
    }

    // IMPORTANT: uses spawn data probability as a parameter
    private bool TryPlaceProp(Prop prop, float spawnProbability, Vector2Int propSize, PropRotation rotation, int x, int y, int topMargin, int bottomMargin, int leftMargin, int rightMargin) {

        bool propUsable = true;
        bool[,] occupiedTiles = new bool[roomWidth * 3, roomHeight * 3]; // accounts for prop placement only
        bool[,] marginTiles = new bool[roomWidth * 3, roomHeight * 3]; // accounts for prop margins only

        if (rotation.GetScanXRight()) {

            if (rotation.GetScanYUp()) { // scanning x right and y up (0 degree rotation)

                for (int xScan = x - leftMargin; xScan < x + propSize.x + rightMargin; xScan++) {

                    for (int yScan = y - bottomMargin; yScan < y + propSize.y + topMargin; yScan++) {

                        if (xScan < 0 || xScan >= marginedInteriorTiles.GetLength(0) || yScan < 0 || yScan >= marginedInteriorTiles.GetLength(1) || !marginedInteriorTiles[xScan, yScan]) { // check if prop is out of bounds or not on a prop tile

                            propUsable = false; // mark prop as not usable
                            break; // break out of loop since prop is not usable

                        } else {

                            if (xScan < x || xScan >= x + propSize.x || yScan < y || yScan >= y + propSize.y) // check if current tile is a margin tile
                                marginTiles[xScan, yScan] = true; // flag as margin tile
                            else
                                occupiedTiles[xScan, yScan] = true; // flag as occupied tile

                        }
                    }

                    if (!propUsable) // check if prop is not usable
                        break; // break out of loop to go to next prop since this prop is not usable

                }
            } else { // scanning x right and y down (270 degree rotation)

                for (int xScan = x - bottomMargin; xScan < x + propSize.x + topMargin; xScan++) {

                    for (int yScan = y - propSize.y - rightMargin; yScan < y + leftMargin; yScan++) {

                        if (xScan < 0 || xScan >= marginedInteriorTiles.GetLength(0) || yScan < 0 || yScan >= marginedInteriorTiles.GetLength(1) || !marginedInteriorTiles[xScan, yScan]) { // check if prop is out of bounds or not on a prop tile

                            propUsable = false; // mark prop as not usable
                            break; // break out of loop since prop is not usable

                        } else {

                            if (xScan < x || xScan >= x + propSize.x || yScan < y - propSize.y || yScan >= y) // check if current tile is a margin tile
                                marginTiles[xScan, yScan] = true; // flag as margin tile
                            else
                                occupiedTiles[xScan, yScan] = true; // flag as occupied tile

                        }
                    }

                    if (!propUsable) // check if prop is not usable
                        break; // break out of loop to go to next prop since this prop is not usable

                }
            }
        } else {

            if (rotation.GetScanYUp()) { // scanning x left and y up (90 degree rotation)

                for (int xScan = x - propSize.x - topMargin; xScan < x + bottomMargin; xScan++) {

                    for (int yScan = y - leftMargin; yScan < y + propSize.y + rightMargin; yScan++) {

                        if (xScan < 0 || xScan >= marginedInteriorTiles.GetLength(0) || yScan < 0 || yScan >= marginedInteriorTiles.GetLength(1) || !marginedInteriorTiles[xScan, yScan]) { // check if prop is out of bounds or not on a prop tile

                            propUsable = false; // mark prop as not usable
                            break; // break out of loop since prop is not usable

                        } else {

                            if (xScan < x - propSize.x || xScan >= x || yScan < y || yScan >= y + propSize.y) // check if current tile is a margin tile
                                marginTiles[xScan, yScan] = true; // flag as margin tile
                            else
                                occupiedTiles[xScan, yScan] = true; // flag as occupied tile

                        }
                    }

                    if (!propUsable) // check if prop is not usable
                        break; // break out of loop to go to next prop since this prop is not usable

                }
            } else { // scanning x left and y down (180 degree rotation)

                for (int xScan = x - propSize.x - rightMargin; xScan < x + leftMargin; xScan++) {

                    for (int yScan = y - propSize.y - bottomMargin; yScan < y + topMargin; yScan++) {

                        if (xScan < 0 || xScan >= marginedInteriorTiles.GetLength(0) || yScan < 0 || yScan >= marginedInteriorTiles.GetLength(1) || !marginedInteriorTiles[xScan, yScan]) { // check if prop is out of bounds or not on a prop tile

                            propUsable = false; // mark prop as not usable
                            break; // break out of loop since prop is not usable

                        } else {

                            if (xScan < x - propSize.x || xScan >= x || yScan < y - propSize.y || yScan >= y) // check if current tile is a margin tile
                                marginTiles[xScan, yScan] = true; // flag as margin tile
                            else
                                occupiedTiles[xScan, yScan] = true; // flag as occupied tile

                        }
                    }

                    if (!propUsable) // check if prop is not usable
                        break; // break out of loop to go to next prop since this prop is not usable

                }
            }
        }

        if (propUsable) { // if prop is usable with this rotation

            float probability = Random.Range(0f, 100f); // probability of this prop spawning on this tile

            if (probability <= spawnProbability) {

                for (int i = 0; i < marginedInteriorTiles.GetLength(0); i++) {

                    for (int j = 0; j < marginedInteriorTiles.GetLength(1); j++) {

                        if (occupiedTiles[i, j]) { // check if tile is occupied

                            occupiedInteriorTiles[i, j] = false; // flag as occupied tile since prop is placed on it
                            marginedInteriorTiles[i, j] = false; // flag as occupied tile since prop is placed on it

                        }

                        if (marginTiles[i, j]) // check if tile is a margin tile
                            marginedInteriorTiles[i, j] = false; // flag as margin tile

                    }
                }

                InstantiateProp(prop, new Vector3Int(x, y, 0), rotation); // instantiate prop
                return true; // return true because prop was placed

            }
        }

        return false; // return false because prop was not placed

    }

    private void GeneratePlayerSpawn() {

        // at this point, all props have been placed and the player spawn point can be generated based on the prop tiles that are still available

        List<Vector3Int> possibleSpawnPoints = new List<Vector3Int>();

        for (int x = 0; x < occupiedInteriorTiles.GetLength(0); x++) // iterate through x values of open interior tiles
            for (int y = 0; y < occupiedInteriorTiles.GetLength(1); y++) // iterate through y values of open interior tiles
                if (occupiedInteriorTiles[x, y]) // check if tile is an open interior tile
                    possibleSpawnPoints.Add(new Vector3Int(x, y, 0)); // add spawn point to list

        Vector3Int gridSpawnPoint = possibleSpawnPoints[Random.Range(0, possibleSpawnPoints.Count)]; // set grid spawn point random spawn point
        playerGridSpawn = gridSpawnPoint; // set player spawn point to grid spawn point
        playerWorldSpawn = borderTilemap.transform.position + propTilemap.GetCellCenterWorld(gridSpawnPoint); // set world spawn point to center of grid spawn point

    }

    private void InstantiateProp(Prop prop, Vector3Int cellPosition, PropRotation rotation) {

        // create parent object for prop (because props are offset due to the grid) and instantiate it at the bottom left of the current cell with the previously generated rotation
        Transform propParent = new GameObject(prop.name + "Parent").transform;
        propParent.position = borderTilemap.transform.position + propTilemap.GetCellCenterWorld(cellPosition) - new Vector3(propTilemap.cellSize.x / 2f, propTilemap.cellSize.y / 2f, 0f);
        propParent.transform.rotation = rotation.GetRotation();
        propParent.SetParent(propTilemap.transform);

        Instantiate(prop.GetRandomVariation(), propParent); // instantiate prop variation object as a child of the parent object

    }

    public Vector3 GetPlayerSpawn() => playerWorldSpawn;

    public Vector3 GetEnemySpawn(Vector3 gridPosition, int spawnRadius) {

        // choose a random point within the spawn radius of the trash can that is not occupied by a prop
        List<Vector3Int> possibleSpawnPoints = new List<Vector3Int>();

        for (int x = (int) gridPosition.x - spawnRadius; x < (int) gridPosition.x + spawnRadius; x++) // iterate through x values of open interior tiles
            for (int y = (int) gridPosition.y - spawnRadius; y < (int) gridPosition.y + spawnRadius; y++) // iterate through y values of open interior tiles
                if (occupiedInteriorTiles[x, y] && new Vector3(x, y) != playerGridSpawn) // check if tile is an open interior tile and not the player spawn point
                    possibleSpawnPoints.Add(new Vector3Int(x, y, 0)); // add spawn point to list

        Vector3Int gridSpawnPoint = possibleSpawnPoints[Random.Range(0, possibleSpawnPoints.Count)]; // set grid spawn point random spawn point
        return borderTilemap.transform.position + propTilemap.GetCellCenterWorld(gridSpawnPoint); // set world spawn point to center of grid spawn point

    }

    private void OnDrawGizmosSelected() {

        if (expansionsEnabled) {

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawCube(borderTilemap.transform.position + new Vector3(roomWidth * 1.5f, roomHeight * 1.5f), new Vector3(roomWidth * 3, roomHeight * 3)); // draw expansion bounds gizmo

            // BOTTOM LEFT AND TOP RIGHT EXPANSION BOUND GIZMOS:
            //Gizmos.DrawCube(borderTilemap.transform.position + Vector3.zero + borderTilemap.layoutGrid.cellSize / 2f, Vector3.one); // draw bottom left expansion bound gizmo
            //Gizmos.DrawCube(borderTilemap.transform.position + new Vector3Int(baseRoomWidth * 3, baseRoomHeight * 3) - borderTilemap.layoutGrid.cellSize / 2f, Vector3.one); // draw top right expansion bound gizmo

        }

        // draw on top of expansion bounds gizmo
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawCube(borderTilemap.transform.position + new Vector3(roomWidth * 1.5f, roomHeight * 1.5f), new Vector3(roomWidth, roomHeight)); // draw base room bounds gizmo

        // BOTTOM LEFT AND TOP RIGHT BASE ROOM BOUND GIZMOS:
        //Gizmos.DrawCube(borderTilemap.transform.position + new Vector3Int(baseRoomWidth, baseRoomHeight) + borderTilemap.layoutGrid.cellSize / 2f, Vector3.one); // draw bottom left base room bound gizmo
        //Gizmos.DrawCube(borderTilemap.transform.position + new Vector3Int(baseRoomWidth * 2, baseRoomHeight * 2) - borderTilemap.layoutGrid.cellSize / 2f, Vector3.one); // draw top right base room bound gizmo

    }

    // used in ProjectileManager
    public Vector2 GetBoundingSize() => new Vector2(roomWidth, roomHeight);

}
