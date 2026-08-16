extends Control

func _ready() -> void:
	for button in get_tree().get_nodes_in_group("Button"):
		button.pressed.connect(on_button_pressed.bind(button))
		button.mouse_exited.connect(mouse_interaction.bind(button,"exited"))
		button.mouse_entered.connect(mouse_interaction.bind(button,"entered"))
		
	pass 
	
func on_button_hover(button: TextureButton, is_hovered: bool) -> void:
	#pega o animation node filho do texturebutton 
	var anim_player: AnimationPlayer = button.get_node_or_null("AnimationPlayer")
	
	if not anim_player:
		return
		
	if is_hovered:
		anim_player.play("hover")
	else:
		anim_player.stop()

func on_button_pressed(button: TextureButton) -> void:
	match button.name:
		#"Play": 
			#var _game: bool = get_tree().change_scene_to_file("res://Scenes/Menu/Main.tscn")
		"Credits":
			var _credits: bool = get_tree().change_scene_to_file("res://scenes/CreditsUI.tscn")
		"Options":
			var _options: bool = get_tree().change_scene_to_file("res://scenes/Options.tscn")
		"Quit":
			get_tree().quit()

func mouse_interaction(button: TextureButton, state: String) -> void:
	match state:
		"exited":
			button.modulate.a = 1
			on_button_hover(button, false)
		"entered":
			button.modulate.a = 0.5
			on_button_hover(button, true)
