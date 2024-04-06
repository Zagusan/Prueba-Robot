extends Control

# Maze generation
signal generateMaze

func _on_generate_pressed():
	var sizeX = $TabContainer/Laberinto/VBox/Size/Options/X
	var sizeY = $TabContainer/Laberinto/VBox/Size/Options/Y
	var sizeToRequest = Vector2i(sizeX.value, sizeY.value)
	
	var timeSlider = $TabContainer/Laberinto/VBox/WaitTime/HBoxContainer/Slider
	var time = timeSlider.value
	
	generateMaze.emit(sizeToRequest, time)

# Control settings
signal toggleMobileControls

func _on_check_button_toggled(toggled_on):
	toggleMobileControls.emit(toggled_on)
