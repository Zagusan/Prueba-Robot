using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class MazeGen : GridMap
{
	[Signal]
	public delegate void ProgressReportEventHandler(int cellsPlaced);
	[Signal]
	public delegate void GenerationDoneEventHandler();

	[Export]
	public Vector2I size = new(20, 20);
	[Export]
	public double waitTime = 0;

	static readonly RandomNumberGenerator randomNumber = new();

	int cellsPlaced = 0;

	// Cell in the GridMap before the processing is done
	public class Tile
	{
		public bool visited = false;
		public bool[] walls = {true, true, true, true};
	}
	// Tile but after the code finished processing it
	public class Cell
	{
		public Vector3I gridMapPosition;
		public WallType wallType;
		public int orientation;
		public bool alreadyPlaced;

		public Cell(Vector3I gridMapPosition, WallType wallType, int orientation, bool alreadyPlaced)
		{
			this.gridMapPosition = gridMapPosition;
			this.wallType = wallType;
			this.orientation = orientation;
			this.alreadyPlaced = alreadyPlaced;
		}
	}

	private Tile[,] grid;
	readonly ConcurrentQueue<Cell> cellQueue = new();

	// Explains how to access the Tile.walls array
	public enum Directions: int
	{
		up = 0,
		right = 1,
		down = 2,
		left = 3
	}
	// Values based on the indexes provided by the MeshLibrary
	public enum WallType: int
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

	private void PlaceFromQueue()
	{
		if (cellQueue.TryDequeue(out Cell currentCell))
		{
			this.SetCellItem(currentCell.gridMapPosition, (int)currentCell.wallType, currentCell.orientation);
			// Adds to cellsPlaced if alreadyPlaced is false
			cellsPlaced += currentCell.alreadyPlaced ? 0 : 1;
		}
	}

	Task generationTask;
	CancellationTokenSource tokenSource;

	public async void OnGenerationRequest(Vector2I requestedSize, double requestedTime)
	{
		if (!cellQueue.IsEmpty)
		{
			return;
		}

		tokenSource = new();

		size = requestedSize;
		waitTime = requestedTime;

		generationTask = new(() => Generate(tokenSource.Token), tokenSource.Token);
		generationTask.Start();

		if(requestedTime == 0)
		{
			// Places cells as soon as possible
			this.SetProcess(true);
		}
		else
		{
            TimeSpan timeSpan = TimeSpan.FromTicks((long)(requestedTime * TimeSpan.TicksPerSecond));
            while (!(cellQueue.IsEmpty && generationTask.IsCompleted))
			{
				PlaceFromQueue();
				EmitSignal(SignalName.ProgressReport, cellsPlaced);

                Task waitTask = Task.Delay(timeSpan, tokenSource.Token);
				try
				{
					await waitTask;
				}
				catch (OperationCanceledException)
                {

				}
				finally
				{
					waitTask.Dispose();
				}
            }
			EmitSignal(SignalName.GenerationDone);
            tokenSource.Dispose();
			cellsPlaced = 0;
        }
		await generationTask;
		// IDisposables should always be cleaned manually
		generationTask.Dispose();
	}

	public void CancelGeneration()
	{
		if (generationTask.IsCompleted)
		{
			tokenSource.Cancel();
		}
		cellQueue.Clear();
		cellsPlaced = 0;
	}

	// Prevents _Process from inmediately running
	public override void _Ready()
	{
		this.SetProcess(false);
	}

	// Deals with the cellQueue
	readonly Stopwatch queueStopwatch = new();
	double ticksPerMs = Stopwatch.Frequency / 10e3;
	public override void _Process(double delta)
	{
		queueStopwatch.Restart();
		while (queueStopwatch.ElapsedTicks / ticksPerMs < 4)
		{
			if (cellQueue.IsEmpty)
			{
				if (generationTask.IsCompleted)
				{
					EmitSignal(SignalName.GenerationDone);
                    tokenSource.Dispose();
                    cellsPlaced = 0;
					this.SetProcess(false);
				}
			}
			else
			{
				PlaceFromQueue();
			}
		}
		EmitSignal(SignalName.ProgressReport, cellsPlaced);
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

	private void QueuePlacement(Tile tile, Vector2I position, bool alreadyPlaced)
	{
		Vector3I gridMapPosition = new(position.X, 0, position.Y);
		WallType type = GetWallType(tile);
		Directions orientation = GetOrientation(tile, type);

		cellQueue.Enqueue(new Cell(gridMapPosition, type, orientations[(int)orientation], alreadyPlaced));
	}

	public void Generate(CancellationToken token)
	{

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
		while(tilesPlaced < gridMagnitude && !token.IsCancellationRequested)
		{
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
				QueuePlacement(currentTile, current, goingBack);
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
					QueuePlacement(currentTile, current, false);
					tilesPlaced++;
				}
				current = stack.Pop();
				goingBack = true;
			}
		}
	}
}