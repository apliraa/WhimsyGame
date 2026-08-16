extends AudioStreamPlayer2D

func _ready() -> void:
	start_two_minute_loop()

func start_two_minute_loop() -> void:
	while is_inside_tree():
		await get_tree().create_timer(120.0).timeout
		play()
