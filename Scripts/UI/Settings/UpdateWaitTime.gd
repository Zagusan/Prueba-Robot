extends Control

@onready var slider = $Options/Slider
@onready var label = $Options/Time

func _on_slider_value_changed(value):
	# var roundedValue := roundf(value * 10e3) * 10e-3
	label.text = str(value) + " segundos"
