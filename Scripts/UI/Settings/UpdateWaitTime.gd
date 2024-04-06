extends Control

@onready var slider = $HBoxContainer/Slider
@onready var label = $HBoxContainer/Time

func _on_slider_value_changed(value):
	label.text = str(value) + " segundos"
