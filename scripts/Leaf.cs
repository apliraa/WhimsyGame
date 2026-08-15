using Godot;
using System;

public partial class Leaf : Sprite2D
{
	[Export] public float ControllerMoveSpeed { get; set; } = 320.0f;
	[Export] public float ControllerRotationSpeed { get; set; } = 3.5f;

	private bool dragging = false;
	private bool controllerControlled = false;
	private Vector2 offset = Vector2.Zero;

	public bool IsControllerControlled => controllerControlled;

	public override void _Ready()
	{
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.LeafFocused += OnAnyLeafFocused;
		}
	}

	public override void _ExitTree()
	{
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.LeafFocused -= OnAnyLeafFocused;
		}
	}

	public override void _Process(double delta)
	{
		if (dragging)
		{
			GlobalPosition = GetGlobalMousePosition() - offset;
		}
	}

	public void BeginControllerControl()
	{
		controllerControlled = true;
		ZIndex = 5;
	}

	public void EndControllerControl()
	{
		controllerControlled = false;
		ZIndex = dragging ? 5 : 0;
	}

	public void MoveWithController(Vector2 input, double delta)
	{
		if (!controllerControlled || input == Vector2.Zero)
		{
			return;
		}

		Vector2 direction = input;
		if (direction.LengthSquared() > 1.0f)
		{
			direction = direction.Normalized();
		}

		GlobalPosition += direction * ControllerMoveSpeed * (float)delta;
		KeepInsideViewport();
	}

	public void RotateWithController(float input, double delta)
	{
		if (!controllerControlled || Mathf.Abs(input) < 0.001f)
		{
			return;
		}

		Rotation += input * ControllerRotationSpeed * (float)delta;
	}

	public bool ContainsGlobalPoint(Vector2 globalPoint)
	{
		if (Texture == null)
		{
			return false;
		}

		Vector2 textureSize = Texture.GetSize();
		Rect2 localBounds = new(-textureSize * 0.5f, textureSize);
		return localBounds.HasPoint(ToLocal(globalPoint));
	}

	public void SetControllerSelected(bool selected)
	{
		ZIndex = selected || dragging || controllerControlled ? 5 : 0;
	}
	
	public void _on_leaf_button_button_down(){
		dragging = true;
		offset = GetGlobalMousePosition() - GlobalPosition;
		ZIndex = 5;

		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LeafFocused, this);
	}
	
	public void _on_leaf_button_button_up(){
		dragging = false;
		
	}
	
	public void OnAnyLeafFocused(Node2D focusedLeaf)
	{
		if(focusedLeaf != this)
		{
			ZIndex = 0;
		}
	}

	private void KeepInsideViewport()
	{
		Rect2 viewport = GetViewportRect();
		Vector2 halfSize = Vector2.Zero;

		if (Texture != null)
		{
			Vector2 textureSize = Texture.GetSize();
			halfSize = new Vector2(
				textureSize.X * Mathf.Abs(GlobalScale.X),
				textureSize.Y * Mathf.Abs(GlobalScale.Y)) * 0.5f;
		}

		float minX = viewport.Position.X + halfSize.X;
		float maxX = viewport.Position.X + viewport.Size.X - halfSize.X;
		float minY = viewport.Position.Y + halfSize.Y;
		float maxY = viewport.Position.Y + viewport.Size.Y - halfSize.Y;

		if (minX > maxX)
		{
			minX = maxX = viewport.Position.X + viewport.Size.X * 0.5f;
		}

		if (minY > maxY)
		{
			minY = maxY = viewport.Position.Y + viewport.Size.Y * 0.5f;
		}

		GlobalPosition = new Vector2(
			Mathf.Clamp(GlobalPosition.X, minX, maxX),
			Mathf.Clamp(GlobalPosition.Y, minY, maxY));
	}
}
