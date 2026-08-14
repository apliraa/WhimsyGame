using Godot;
using System;

public partial class Leaf : Sprite2D
{
	private bool dragging = false;
	private Vector2 offset = new Vector2(0,0);

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (dragging)
		{
			GlobalPosition = GetGlobalMousePosition() - offset;
		}
	}
	
	public void _on_leaf_button_button_down(){
		dragging = true;
		offset = GetGlobalMousePosition() - GlobalPosition;
		GetParent().MoveChild(this, -1);
		ZIndex += 1;
	}
	
	public void _on_leaf_button_button_up(){
		dragging = false;
		ZIndex -=1;
	}
	
}
