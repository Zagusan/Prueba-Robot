extends GridMap

@export var size := Vector2i(20, 20)
@export var waitTime := 0.0
@export var deferred := false
@export var multithreading := false

# RandomNumberGenerator is not needed because of the @GlobalScope functions available

var placementCount := 0

class Tile:
	var visited := false
	var walls := [true, true, true, true]

# Explains how to access the Tile.walls array
enum Directions
{
	up = 0,
	right = 1,
	down = 2,
	left = 3
}
# Values based on the indexes provided by the MeshLibrary
enum WallType
{
	empty = 0,
	hallway = 1,
	single = 2,
	corner = 3,
	deadEnd = 4,
	closed = 5
}
	# It's an array so that I can just use the directions enum, avoiding
	# more converter functions and making it easier to work with
	# Also don't ask me why these values, that's just how GridMaps work
var orientations := [0, 22, 10, 16]

var generate: Thread

# Called when the node enters the scene tree for the first time.
func OnGenerationRequest(requestedSize: Vector2i, requestedTime: float):
	size = requestedSize
	waitTime = requestedTime
	if multithreading:
		generate = Thread.new()
		generate.start(Generate)
	else:
		Generate()

func _exit_tree():
	if multithreading:
		generate.wait_to_finish()

func ToDirection(vector: Vector2i) -> Directions:
	# Doesn't use a match statement because it doesn't work with vectors
	if vector == Vector2i.UP:
		return Directions.up
	elif vector == Vector2i.RIGHT:
		return Directions.right
	elif vector == Vector2i.DOWN:
		return Directions.down
	elif vector == Vector2i.LEFT:
		return Directions.left
	else:
		push_error("Argument Exception: Unknown vector direction")
		return Directions.up

func IsVectorInSizeRange(vector: Vector2i) -> bool:
	# Checks every component individually (The < operator doesn't work for this)
	if vector.x >= 0 and vector.x < size.x and vector.y >= 0 and vector.y < size.y:
		return true
	else:
		return false

func GetUnvisitedNeighbors(grid: Array[Variant], index: Vector2i) -> Array[Vector2i]:
	# Makes a list of all values to check
	var checklist := [
		index + Vector2i.UP,
		index + Vector2i.RIGHT,
		index + Vector2i.DOWN,
		index + Vector2i.LEFT]
	
	var unvisited: Array[Vector2i] = []
	
	for currentPosition: Vector2i in checklist:
		# Checks if the value is out of bounds
		if !IsVectorInSizeRange(currentPosition):
			continue
		
		var currentTile: Tile = grid[currentPosition.x][currentPosition.y]
		
		if !currentTile.visited:
			unvisited.append(currentPosition)
	return unvisited

func GetWallType(tile: Tile) -> WallType:
	var wallCount := tile.walls.count(true)
	match wallCount:
		0:
			return WallType.empty
		1:
			return WallType.single
		2:
			# If the two walls opposite to each other are the same, it's a hallway.
			# If they are adjacent, it's a corner.
			if tile.walls[Directions.up] == tile.walls[Directions.down]:
				return WallType.hallway
			else:
				return WallType.corner
		3:
			return WallType.deadEnd
		4:
			return WallType.closed
		_:
			push_error("Argument Exception: Invalid wall configuration")
			return WallType.empty

func GetOrientation(tile: Tile, type: WallType) -> Directions:
	match type:
		WallType.empty, WallType.closed:
			# Orientation doesn't matter, so it just returns up
			return Directions.up
		WallType.single:
			# Returns the index of the first (and only) element which has a wall, casted to Directions
			return (tile.walls.find(true)) as Directions
		WallType.hallway:
			# In hallway, the walls will always be opposite to each other
			if tile.walls[Directions.up] == true:
				return Directions.up
			else:
				return Directions.right
		WallType.corner:
			var index := tile.walls.find(true)
			# If the index is 0, checks the last element of the array to properly rotate it
			# Else, it returns the index right away, casted to Directions
			# The orientation is determined by the first wall from left to right
			if index == 0 and tile.walls[-1]:
				return (tile.walls.size() - 1) as Directions
			else:
				return index as Directions
		WallType.deadEnd:
			# Returns the index of the first (and only) element which doesn't have a wall, casted to Directions
			# WallType.deadEnd is reversed (in the MeshLibrary) relative to the others to make it easier to work with
			return (tile.walls.find(false)) as Directions
		_:
			push_error("Argument Exception: Invalid wall configuration")
			return Directions.up

func Timer(time: float) -> Signal:
	return get_tree().create_timer(time).timeout

func PlaceTileDeferred(tile: Tile, placementPosition: Vector2i):
	var gridMapPosition := Vector3i(placementPosition.x, 0, placementPosition.y)
	var type := GetWallType(tile)
	var orientation := GetOrientation(tile, type)
	
	if deferred:
		call_deferred("set_cell_item", gridMapPosition, type, orientations[orientation])
	else:
		set_cell_item(gridMapPosition, type, orientations[orientation])
	placementCount += 1

func Generate():
	# var start := Time.get_ticks_usec()
	
	clear()
	
	var grid: Array[Variant] = []
	var stack: Array[Vector2i] = []
	
	# Unlike in C#, every instance needs to be created beforehand
	for x in range(size.x):
		grid.append([])
		for y in range(size.y):
			grid[x].append(Tile.new())
	
	# Picks the first index
	var current := Vector2i(randi_range(0, size.x - 1), randi_range(0, size.y - 1))
	# Configures the first tile
	grid[current.x][current.y].visited = true
	
	var gridMagnitude := size.x * size.y
	var tilesPlaced := 0
	var goingBack := false
	# Iterates through all of the 2D array
	while tilesPlaced < gridMagnitude:
		# UX choice so that the user has time to get into position
		if waitTime > 0:
			await Timer(waitTime)
			
		var currentTile: Tile = grid[current.x][current.y]
		
		var unvisitedNeighbors := GetUnvisitedNeighbors(grid, current)
		if unvisitedNeighbors.size() != 0:
			# Adds current to the stack now because it's going to change later
			stack.append(current)
			
			var next := unvisitedNeighbors[randi_range(0, unvisitedNeighbors.size() - 1)]
			var nextTile: Tile = grid[next.x][next.y]
			
			var nextDirection := next - current
			
			currentTile.walls[ToDirection(nextDirection)] = false
			nextTile.walls[ToDirection(-nextDirection)] = false
			
			nextTile.visited = true
			
			# Needs to be run here because there can't be further updates once a tile is placed
			PlaceTileDeferred(currentTile, current)
			# Only adds to tiles placed if not going back
			if !goingBack:
				tilesPlaced += 1
			
			current = next
			goingBack = false
		else:
			# Places the current tile if it's the first time going back
			if !goingBack:
				PlaceTileDeferred(currentTile, current)
				tilesPlaced += 1
			current = stack.pop_back()
			goingBack = true
	# print("Maze generated in ", (Time.get_ticks_usec() - start) * 10e-6, " seconds")
	print("Placed ", placementCount, " tiles since the start of the program")

