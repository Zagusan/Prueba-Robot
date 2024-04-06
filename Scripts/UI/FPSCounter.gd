extends Label

func _process(delta):
	text = "FPS: " + str(roundf(1 / delta))
