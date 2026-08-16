extends AudioStreamPlayer2D

func _ready() -> void:
	start_one_minute_loop()

func start_one_minute_loop() -> void:
	while is_inside_tree():
		await get_tree().create_timer(60.0).timeout
		play()
