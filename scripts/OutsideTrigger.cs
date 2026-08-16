using Godot;

public partial class OutsideTrigger : Area2D
{
	[Export] public float OutsideMargin { get; set; } = 6.0f;
	[Export] public float BoundarySize { get; set; } = 4096.0f;

	private CollisionShape2D top;
	private CollisionShape2D bottom;
	private CollisionShape2D left;
	private CollisionShape2D right;

	public bool IsActive { get; private set; }

	public override void _Ready()
	{
		top = GetNode<CollisionShape2D>("Top");
		bottom = GetNode<CollisionShape2D>("Bottom");
		left = GetNode<CollisionShape2D>("Left");
		right = GetNode<CollisionShape2D>("Right");
		SetActive(Visible);
	}

	public void ConfigureForTarget(FormTarget target)
	{
		if (target == null)
		{
			return;
		}

		Vector2 targetSize = target.GetGlobalTargetSize();
		if (targetSize == Vector2.Zero)
		{
			return;
		}

		GlobalPosition = target.GlobalPosition;
		GlobalRotation = target.GlobalRotation;
		GlobalScale = Vector2.One;

		float boundary = BoundarySize > 0.0f ? BoundarySize : 4096.0f;
		float halfWidth = targetSize.X * 0.5f + OutsideMargin;
		float halfHeight = targetSize.Y * 0.5f + OutsideMargin;

		ConfigureShape(
			top,
			new Vector2(boundary, boundary),
			new Vector2(0.0f, -halfHeight - boundary * 0.5f));
		ConfigureShape(
			bottom,
			new Vector2(boundary, boundary),
			new Vector2(0.0f, halfHeight + boundary * 0.5f));
		ConfigureShape(
			left,
			new Vector2(boundary, targetSize.Y + OutsideMargin * 2.0f),
			new Vector2(-halfWidth - boundary * 0.5f, 0.0f));
		ConfigureShape(
			right,
			new Vector2(boundary, targetSize.Y + OutsideMargin * 2.0f),
			new Vector2(halfWidth + boundary * 0.5f, 0.0f));
	}

	public void SetActive(bool active)
	{
		IsActive = active;
		Visible = active;
		Monitoring = active;
		Monitorable = active;

		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape2D collisionShape)
			{
				collisionShape.Disabled = !active;
			}
		}
	}

	private void ConfigureShape(
		CollisionShape2D collisionShape,
		Vector2 size,
		Vector2 position)
	{
		if (collisionShape.Shape is RectangleShape2D rectangle)
		{
			rectangle.Size = size;
		}

		collisionShape.Position = position;
	}
}
