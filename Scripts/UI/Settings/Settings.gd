extends Control

@onready var mazeGen = get_tree().root.get_node("Maze/MazeGen")

@onready var generateButton: Button = $TabContainer/Laberinto/Container/Options/Generate
@onready var progressOptions: HBoxContainer = $TabContainer/Laberinto/Container/Options/Progress
@onready var progressBar: ProgressBar = $TabContainer/Laberinto/Container/Options/Progress/ProgressBar

func _ready():
	generateButton.visible = true
	progressOptions.visible = false

# Maze generation
signal generateMaze
signal cancelGeneration

var generationDone := false

func _on_generate_pressed():
	generationDone = false
	
	var sizeX = $TabContainer/Laberinto/Container/Size/Options/X
	var sizeY = $TabContainer/Laberinto/Container/Size/Options/Y
	var sizeToRequest = Vector2i(sizeX.value, sizeY.value)
	
	var timeSlider = $TabContainer/Laberinto/Container/WaitTime/Options/Slider
	var time = timeSlider.value
	
	progressBar.max_value = sizeToRequest.x * sizeToRequest.y
	
	generateMaze.emit(sizeToRequest, time)
	
	await Signal(mazeGen, "ProgressReport")
	# Prevents problems if signals get sent in the wrong order
	if !generationDone:
		generateButton.visible = false
		progressOptions.visible = true

func _on_maze_gen_progress_report(cellsPlaced):
	progressBar.value = cellsPlaced

func _on_maze_gen_generation_done():
	generationDone = true
	generateButton.visible = true
	progressOptions.visible = false

func _on_cancel_pressed():
	cancelGeneration.emit()

# Control settings
signal toggleMobileControls

func _on_check_button_toggled(toggled_on):
	toggleMobileControls.emit(toggled_on)
