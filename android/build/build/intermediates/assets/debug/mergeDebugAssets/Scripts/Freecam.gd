extends Camera3D

@export var speed := 10
@export var sensitivity := 0.1
# 180°
@export var rotationRangeRadians := PI / 2

var cameraMovement := Vector2.ZERO

# Called when the node enters the scene tree for the first time.
func _ready():
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func KeyPressed(input: InputEventKey) -> void:
	# Unlocks the mouse when ESC is pressed
	if input.is_action("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func MousePressed(input: InputEventMouseButton) -> void:
	# Captures the mouse when LMB is pressed
	if input.is_action("LMB") and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func MouseMoved(input: InputEventMouseMotion) -> void:
	if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		cameraMovement -= input.relative * sensitivity

func ScreenDragged(input: InputEventScreenDrag):
	cameraMovement -= input.relative * sensitivity

func _unhandled_input(input: InputEvent):
	# Runs different code depending on the input to avoid wasting resources
	match input.get_class():
		"InputEventKey":
			KeyPressed(input)
		"InputEventMouseButton":
			MousePressed(input)
		"InputEventMouseMotion":
			MouseMoved(input)
		"InputEventScreenDrag":
			ScreenDragged(input)

func GetPlayerMovement() -> Vector3:
	var movement := Vector3(
		Input.get_axis("Left", "Right"),
		Input.get_axis("Down", "Up"),
		Input.get_axis("Forward", "Back")
		)
	return speed * movement.normalized()

func _process(delta: float):
	var playerMovement := GetPlayerMovement() * delta
	# Moves the camera in object space (except upwards)
	translate(Vector3(playerMovement.x, 0, playerMovement.z))
	# Moves only upwards in world space
	position.y += playerMovement.y
	# Rotates in object space the amount that the mouse was moved
	rotation += Vector3(cameraMovement.y, cameraMovement.x, 0)
	# Prevents camera from rotating too much
	rotation.x = clamp(rotation.x, -rotationRangeRadians, rotationRangeRadians)
	rotation.z = clamp(rotation.z, -rotationRangeRadians, rotationRangeRadians)
	cameraMovement = Vector2.ZERO
