using Godot;
using System;

public partial class Leaf : Sprite2D
{
	[Export] public float ControllerMoveSpeed { get; set; } = 320.0f;
	[Export(PropertyHint.Range, "0,10,0.05")]
	public float ControllerRotationSpeed { get; set; } = 3.5f;
	[Export(PropertyHint.Range, "0,10,0.05")]
	public float MouseRotationSpeed { get; set; } = 1.0f;

	private bool mouseControlled = false;
	private bool controllerControlled = false;
	private Area2D leafHitbox;
	private int formOverlapCount;
	private int outsideOverlapCount;

	public bool IsControllerControlled => controllerControlled;
	public bool dentro_da_forma { get; private set; }
	public bool fora_da_folha { get; private set; }

	public override void _Ready()
	{
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.LeafFocused += OnAnyLeafFocused;
		}

		leafHitbox = GetNodeOrNull<Area2D>("LeafHitbox");
		if (leafHitbox != null)
		{
			leafHitbox.AreaEntered += OnLeafHitboxAreaEntered;
			leafHitbox.AreaExited += OnLeafHitboxAreaExited;
		}
	}

	public override void _ExitTree()
	{
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.LeafFocused -= OnAnyLeafFocused;
		}

		if (leafHitbox != null)
		{
			leafHitbox.AreaEntered -= OnLeafHitboxAreaEntered;
			leafHitbox.AreaExited -= OnLeafHitboxAreaExited;
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
		ZIndex = mouseControlled ? 5 : 0;
	}

	public void BeginMouseControl()
	{
		mouseControlled = true;
		ZIndex = 5;
	}

	public void EndMouseControl()
	{
		mouseControlled = false;
		ZIndex = controllerControlled ? 5 : 0;
	}

	public void MoveWithMouse(Vector2 globalPosition)
	{
		if (!mouseControlled)
		{
			return;
		}

		GlobalPosition = globalPosition;
		KeepInsideViewport();
	}

	public void RotateWithMouse(double delta)
	{
		if (!mouseControlled)
		{
			return;
		}

		Rotation += MouseRotationSpeed * (float)delta;
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
		CollisionShape2D collisionShape = GetNodeOrNull<CollisionShape2D>(
			"LeafHitbox/CollisionShape2D");
		if (collisionShape != null && collisionShape.Shape is ConvexPolygonShape2D convexPolygon)
		{
			return IsPointInsideConvexPolygon(
				collisionShape.ToLocal(globalPoint),
				convexPolygon.Points);
		}

		if (collisionShape != null && collisionShape.Shape is RectangleShape2D rectangle)
		{
			Rect2 localBounds = new(-rectangle.Size * 0.5f, rectangle.Size);
			return localBounds.HasPoint(collisionShape.ToLocal(globalPoint));
		}

		if (Texture == null)
		{
			return false;
		}

		Vector2 textureSize = Texture.GetSize();
		Rect2 textureBounds = new(-textureSize * 0.5f, textureSize);
		return textureBounds.HasPoint(ToLocal(globalPoint));
	}

	private bool IsPointInsideConvexPolygon(Vector2 point, Vector2[] polygon)
	{
		bool hasPositiveCross = false;
		bool hasNegativeCross = false;

		for (int index = 0; index < polygon.Length; index++)
		{
			Vector2 current = polygon[index];
			Vector2 next = polygon[(index + 1) % polygon.Length];
			float cross = (next - current).Cross(point - current);

			if (cross > 0.001f)
			{
				hasPositiveCross = true;
			}
			else if (cross < -0.001f)
			{
				hasNegativeCross = true;
			}

			if (hasPositiveCross && hasNegativeCross)
			{
				return false;
			}
		}

		return true;
	}

	public void ResetPhaseDetection()
	{
		formOverlapCount = 0;
		outsideOverlapCount = 0;
		UpdatePhaseFlags();
	}

	public void RefreshPhaseDetection()
	{
		formOverlapCount = 0;
		outsideOverlapCount = 0;

		if (leafHitbox != null)
		{
			foreach (Area2D area in leafHitbox.GetOverlappingAreas())
			{
				RegisterPhaseArea(area);
			}
		}

		UpdatePhaseFlags();
	}

	public void SetControllerSelected(bool selected)
	{
		ZIndex = selected || mouseControlled || controllerControlled ? 5 : 0;
	}
	
	public void OnAnyLeafFocused(Node2D focusedLeaf)
	{
		if (focusedLeaf != this && !mouseControlled && !controllerControlled)
		{
			ZIndex = 0;
		}
	}

	private void OnLeafHitboxAreaEntered(Area2D area)
	{
		RegisterPhaseArea(area);
		UpdatePhaseFlags();
	}

	private void OnLeafHitboxAreaExited(Area2D area)
	{
		if (area.IsInGroup("form_hitboxes"))
		{
			formOverlapCount = Mathf.Max(0, formOverlapCount - 1);
		}

		if (area.IsInGroup("outside_triggers"))
		{
			outsideOverlapCount = Mathf.Max(0, outsideOverlapCount - 1);
		}

		UpdatePhaseFlags();
	}

	private void RegisterPhaseArea(Area2D area)
	{
		if (area.IsInGroup("form_hitboxes"))
		{
			formOverlapCount++;
		}

		if (area.IsInGroup("outside_triggers"))
		{
			outsideOverlapCount++;
		}
	}

	private void UpdatePhaseFlags()
	{
		dentro_da_forma = formOverlapCount > 0;
		fora_da_folha = outsideOverlapCount > 0;
	}

	private void KeepInsideViewport()
	{
		Rect2 viewport = GetViewportRect();
		Vector2 halfSize = GetHitboxHalfSize();

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

	private Vector2 GetHitboxHalfSize()
	{
		CollisionShape2D collisionShape = GetNodeOrNull<CollisionShape2D>(
			"LeafHitbox/CollisionShape2D");
		if (collisionShape != null && collisionShape.Shape is RectangleShape2D rectangle)
		{
			return new Vector2(
				rectangle.Size.X * Mathf.Abs(GlobalScale.X),
				rectangle.Size.Y * Mathf.Abs(GlobalScale.Y)) * 0.5f;
		}

		if (Texture != null)
		{
			Vector2 textureSize = Texture.GetSize();
			return new Vector2(
				textureSize.X * Mathf.Abs(GlobalScale.X),
				textureSize.Y * Mathf.Abs(GlobalScale.Y)) * 0.5f;
		}

		return Vector2.Zero;
	}
}
