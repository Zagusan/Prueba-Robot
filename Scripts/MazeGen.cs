using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Godot.Control;
using static Godot.TextServer;
using static System.Net.Mime.MediaTypeNames;

public partial class MazeGen : GridMap
{

	[Export]
	public Vector2I size = new(20, 20);
	[Export]
	public double waitTime = 0;
	[Export]
	public bool deferred = false;
	[Export]
	public bool multithreading = false;

	static readonly RandomNumberGenerator randomNumber = new();

	uint placementCount = 0;

	public class Tile
	{
		public bool visited = false;
		public bool[] walls = {true, true, true, true};
	}

    private Tile[,] grid;

    // Explains how to access the Tile.walls array
    enum Directions: int
	{
		up = 0,
		right = 1,
		down = 2,
		left = 3
	}
	// Values based on the indexes provided by the MeshLibrary
	enum WallType: int
	{
		empty = 0,
		hallway = 1,
		single = 2,
		corner = 3,
		deadEnd = 4,
		closed = 5
	}
	/* It's an array so that I can just use the directions enum, avoiding
	 * more converter functions and making it easier to work with
	 * Also don't ask me why these values, that's just how GridMaps work */
	int[] orientations = {0, 22, 10, 16};

	Thread generate;

	public void OnGenerationRequest(Vector2I requestedSize, double requestedTime)
	{
		size = requestedSize;
		waitTime = requestedTime;
		if (multithreading)
		{
            generate = new(Generate);
            generate.Start();
        }
		else
		{
            Generate();
        }
	}

    public override void _ExitTree()
    {
        if (multithreading)
		{
			generate.Join();
		}
    }

    private static Directions ToDirection(Vector2I vector)
	{
		// Doesn't use a switch statement because it doesn't work with vectors
		if (vector == Vector2I.Up)
		{
			return Directions.up;
		}
		else if (vector == Vector2I.Right)
		{
			return Directions.right;
		}
		else if (vector == Vector2I.Down)
		{
			return Directions.down;
		}
		else if (vector == Vector2I.Left)
			return Directions.left;
		else
		{
			throw new ArgumentException("Unknown vector direction");
		}
	}

	private bool IsVectorInSizeRange(Vector2I vector)
	{
        // Checks every component individually (The < operator doesn't work for this)
        if (vector.X >= 0 && vector.X < size.X && vector.Y >= 0 && vector.Y < size.Y)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private List<Vector2I> GetUnvisitedNeighbors(Tile[,] grid, Vector2I index)
	{
		// Makes a list of all values to check
		Vector2I[] checklist = 
		{
			index + Vector2I.Up,
			index + Vector2I.Right,
			index + Vector2I.Down,
			index + Vector2I.Left
		};

		List<Vector2I> unvisited = new();

		foreach(Vector2I position in checklist)
		{
			// Checks if value is out of bounds
			if (!IsVectorInSizeRange(position))
			{
				continue;
			}

			// Creates a tile if it doesn't exist
			if (grid[position.X, position.Y] == null)
			{
				grid[position.X, position.Y] = new();
			}

			Tile currentTile = grid[position.X, position.Y];

			if (!currentTile.visited)
			{
				unvisited.Add(position);
			}
		}
		return unvisited;
	}

	private static WallType GetWallType(Tile tile)
	{
		int wallCount = tile.walls.Count(wall => wall == true);
		switch (wallCount)
		{
			case 0:
				return WallType.empty;
			case 1:
				return WallType.single;
			case 2:
				/* If the two walls opposite to each other are the same, it's a hallway.
				 * If they are adjacent, it's a corner. */
		if (tile.walls[(int)Directions.up] == tile.walls[(int)Directions.down])
				{
					return WallType.hallway;
				}
				else
				{
					return WallType.corner;
				}
			case 3:
				return WallType.deadEnd;
			case 4:
				return WallType.closed;
			default:
				throw new ArgumentException("Invalid wall configuration");
		}
	}

	private static Directions GetOrientation(Tile tile, WallType type)
	{
		switch(type)
		{
			case WallType.empty: case WallType.closed:
				// Orientation doesn't matter here, so it just returns up
				return Directions.up;
			case WallType.single:
				// Returns the index of the first (and only) element which has a wall, casted to Directions
				return (Directions)Array.FindIndex(tile.walls, wall => wall == true);
			case WallType.hallway:
				// In hallway, the walls will always be opposite to each other
				if (tile.walls[(int)Directions.up] == true)
				{
					return Directions.up;
				}
				else
				{
					return Directions.right;
				}
			case WallType.corner:
				int index = Array.FindIndex(tile.walls, wall => wall == true);
				/* If the index is 0, checks the last element of the array to properly rotate it
				 * Else, it returns the index right away, casted to Directions
				 * The orientation is determined by the first wall from left to right */
				if(index == 0 && tile.walls[^1])
				{
					return (Directions)(tile.walls.Length - 1);
				}
				else
				{
					return (Directions)index;
				}
			case WallType.deadEnd:
				/* Returns the index of the first (and only) element which doesn't have a wall, casted to Directions
				 * WallType.deadEnd is reversed (in the MeshLibrary) relative to the others to make it easier to work with */
				return (Directions)Array.FindIndex(tile.walls, wall => wall == false);
			default:
				throw new ArgumentException("Invalid wall configuration");
		}
	}

	private SignalAwaiter Timer(double time)
	{
		return ToSignal(GetTree().CreateTimer(time), "timeout");
	}

	private void PlaceTileDeferred(Tile tile, Vector2I position)
	{
		Vector3I gridMapPosition = new(position.X, 0, position.Y);
		WallType type = GetWallType(tile);
		Directions orientation = GetOrientation(tile, type);

		if(deferred)
		{
			this.CallDeferred(MethodName.SetCellItem, gridMapPosition, (int)type, orientations[(int)orientation]);
		}
		else
		{
			SetCellItem(gridMapPosition, (int)type, orientations[(int)orientation]);
		}
		placementCount++;
    }

	public async void Generate()
	{
		// ulong start = Time.GetTicksUsec();

		this.Clear();

		grid = new Tile[size.X, size.Y];
		Stack<Vector2I> stack = new();

		// Picks the first index
		Vector2I current = new(randomNumber.RandiRange(0, size.X - 1), randomNumber.RandiRange(0, size.Y - 1));
		// Initializes the first tile
		grid[current.X, current.Y] = new()
		{
			visited = true
		};

		int gridMagnitude = size.X * size.Y;
		int tilesPlaced = 0;
		bool goingBack = false;
		// Iterates through all of the 2D array
		while(tilesPlaced < gridMagnitude)
		{
			// UX choice so that the user has time to get into position
			if (waitTime > 0)
			{
				await Timer(waitTime);
			}

			Tile currentTile = grid[current.X, current.Y];

			List<Vector2I> unvisitedNeighbors = GetUnvisitedNeighbors(grid, current);
			if (unvisitedNeighbors.Count != 0)
			{
				// Adds current to the stack now because it's going to change later
				stack.Push(current);

				Vector2I next = unvisitedNeighbors[randomNumber.RandiRange(0, unvisitedNeighbors.Count - 1)];
				Tile nextTile = grid[next.X, next.Y];

				Vector2I nextDirection = next - current;

				currentTile.walls[(int)ToDirection(nextDirection)] = false;
				nextTile.walls[(int)ToDirection(-nextDirection)] = false;

				nextTile.visited = true;

				// Needs to be run here because there can't be further updates to the tile once placed
				PlaceTileDeferred(currentTile, current);
                // Only adds to tiles placed if not going back
                tilesPlaced += goingBack ? 0 : 1;

				current = next;
				goingBack = false;
			}
			else
			{
				// Places the current tile if it's the first time going back
				if (!goingBack)
				{
					PlaceTileDeferred(currentTile, current);
					tilesPlaced++;
				}
				current = stack.Pop();
				goingBack = true;
			}
		}
        // GD.Print("Maze generated in " + (Time.GetTicksUsec() - start) * 10e-6 + " seconds");
        GD.Print("Placed " + placementCount, " tiles since the start of the program");
	}
}