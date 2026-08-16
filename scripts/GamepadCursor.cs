using Godot;

public partial class GamepadCursor : Node2D
{
	[Export] public float MoveSpeed { get; set; } = 500.0f;

	public override void _Ready()
	{
		KeepInsideViewport();
	}

	public void MoveWithController(Vector2 input, double delta)
	{
		if (input == Vector2.Zero)
		{
			return;
		}

		Vector2 direction = input;
		if (direction.LengthSquared() > 1.0f)
		{
			direction = direction.Normalized();
		}

		GlobalPosition += direction * MoveSpeed * (float)delta;
		KeepInsideViewport();
	}

	private void KeepInsideViewport()
	{
		Rect2 viewport = GetViewportRect();
		Vector2 halfSize = Vector2.Zero;
		ColorRect placeholder = GetNodeOrNull<ColorRect>("Placeholder");

		if (placeholder != null)
		{
			halfSize = placeholder.Size * 0.5f;
		}

		float minX = viewport.Position.X + halfSize.X;
		float maxX = viewport.Position.X + viewport.Size.X - halfSize.X;
		float minY = viewport.Position.Y + halfSize.Y;
		float maxY = viewport.Position.Y + viewport.Size.Y - halfSize.Y;

		GlobalPosition = new Vector2(
			Mathf.Clamp(GlobalPosition.X, minX, maxX),
			Mathf.Clamp(GlobalPosition.Y, minY, maxY));
	}
}
