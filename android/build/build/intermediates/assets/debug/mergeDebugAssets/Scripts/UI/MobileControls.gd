extends Control

# Shows the UI when toggled
func _on_settings_toggle_mobile_controls(shouldBeVisible):
	visible = shouldBeVisible

# Up-Left button
func _on_up_left_button_down():
	Input.action_press("Forward")
	Input.action_press("Left")
func _on_up_left_button_up():
	Input.action_release("Forward")
	Input.action_release("Left")

# Forward button
func _on_up_button_down():
	Input.action_press("Forward")
func _on_up_button_up():
	Input.action_release("Forward")

# Up-Right button
func _on_up_right_button_down():
	Input.action_press("Forward")
	Input.action_press("Right")
func _on_up_right_button_up():
	Input.action_release("Forward")
	Input.action_release("Right")

# Left button
func _on_left_button_down():
	Input.action_press("Left")
func _on_left_button_up():
	Input.action_release("Left")

# Center button (does nothing for now)
func _on_center_button_down():
	pass
func _on_center_button_up():
	pass # Replace with function body.

# Right button
func _on_right_button_down():
	Input.action_press("Right")
func _on_right_button_up():
	Input.action_release("Right")

# Down-Left button
func _on_down_left_button_down():
	Input.action_press("Back")
	Input.action_press("Left")
func _on_down_left_button_up():
	Input.action_release("Back")
	Input.action_release("Left")

# Down button
func _on_down_button_down():
	Input.action_press("Back")
func _on_down_button_up():
	Input.action_release("Back")

# Down-Right button
func _on_down_right_button_down():
	Input.action_press("Back")
	Input.action_press("Right")
func _on_down_right_button_up():
	Input.action_release("Back")
	Input.action_release("Right")

# Height Increase button
func _on_increase_button_down():
	Input.action_press("Up")
func _on_increase_button_up():
	Input.action_release("Up")

# Height Decrease button
func _on_decrease_button_down():
	Input.action_press("Down")
func _on_decrease_button_up():
	Input.action_release("Down")
