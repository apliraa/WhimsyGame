using Godot;
using System;

public partial class Leaf : Sprite2D
{
	[Export] public float ControllerMoveSpeed { get; set; } = 320.0f;
	[Export(PropertyHint.Range, "0,10,0.05")]
	public float ControllerRotationSpeed { get; set; } = 3.5f;
	[Export(PropertyHint.Range, "0,10,0.05")]
	public float MouseRotationSpeed { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.05,1,0.01")]
	public float PlacementAnimationDuration { get; set; } = 0.16f;
	[Export(PropertyHint.Range, "0,20,0.5")]
	public float PlacementAnimationLift { get; set; } = 3.0f;

	private bool mouseControlled = false;
	private bool controllerControlled = false;
	private Area2D leafHitbox;
	private CollisionShape2D collisionShape;
	private CollisionPolygon2D collisionPolygon;
	private int formOverlapCount;
	private int outsideOverlapCount;
	private static int nextStackOrder;
	//private static int nextPlacedStackOrder = 10;
	//private const int ControlledStackOrder = 15;
	// Guarda a ordem antes da folha pular para o topo
	public int OldStackOrder; //teste
	private int initialStackOrder;
	private int stackOrder;
	//private bool hasBeenPlaced;
	private Tween placementTween;
	private Vector2 baseOffset;

	public bool IsMouseControlled => mouseControlled;
	public bool IsControllerControlled => controllerControlled;
	public bool IsBeingControlled => mouseControlled || controllerControlled;
	public bool dentro_da_forma { get; private set; }
	public bool fora_da_folha { get; private set; }

	public override void _Ready()
	{
		initialStackOrder = nextStackOrder++;
		stackOrder = initialStackOrder;
		ZIndex = stackOrder;
		baseOffset = Offset;

		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.LeafFocused += OnAnyLeafFocused;
		}

		leafHitbox = GetNodeOrNull<Area2D>("LeafHitbox");
		collisionShape = GetNodeOrNull<CollisionShape2D>(
			"LeafHitbox/CollisionShape2D");
		collisionPolygon = GetNodeOrNull<CollisionPolygon2D>(
			"LeafHitbox/CollisionPolygon2D");

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
		CancelPlacementAnimation();
		controllerControlled = true;
		BringToFront();
	}

	public void EndControllerControl()
	{
		bool wasControllerControlled = controllerControlled;
		controllerControlled = false;

		// if (wasControllerControlled)
		// {
		// 	RegisterFirstPlacement();
		// }

		//ZIndex = mouseControlled ? ControlledStackOrder : stackOrder;
	}

	public void BeginMouseControl()
	{
		CancelPlacementAnimation();
		mouseControlled = true;
		BringToFront();
	}

	public void EndMouseControl()
	{
		bool wasMouseControlled = mouseControlled;
		mouseControlled = false;

		// if (wasMouseControlled)
		// {
		// 	RegisterFirstPlacement();
		// } 

		//ZIndex = controllerControlled ? ControlledStackOrder : stackOrder;

		if (wasMouseControlled)
		{
			PlayPlacementAnimation();
		}
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
		Vector2[] localPoints = GetLocalHitboxPoints();
		if (localPoints.Length < 3)
		{
			return false;
		}

		Node2D geometryNode = collisionPolygon != null
			? collisionPolygon
			: collisionShape;
		if (geometryNode == null)
		{
			return false;
		}

		return IsPointInsidePolygon(
			geometryNode.ToLocal(globalPoint),
			localPoints);
	}

	public Vector2[] GetGlobalHitboxPoints()
	{
		Vector2[] localPoints = GetLocalHitboxPoints();
		if (localPoints.Length == 0)
		{
			return new Vector2[0];
		}

		Node2D geometryNode = collisionPolygon != null
			? collisionPolygon
			: collisionShape;
		if (geometryNode == null)
		{
			return new Vector2[0];
		}

		Vector2[] globalPoints = new Vector2[localPoints.Length];
		for (int index = 0; index < localPoints.Length; index++)
		{
			globalPoints[index] = geometryNode.ToGlobal(localPoints[index]);
		}

		return globalPoints;
	}

	public void ResetPhaseDetection()
	{
		formOverlapCount = 0;
		outsideOverlapCount = 0;
		dentro_da_forma = false;
		fora_da_folha = false;
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

		UpdatePhaseFlagsFromPhysics();
	}

	public void RefreshPhaseDetection(FormTarget target)
	{
		RefreshPhaseDetection(target, target?.GetGlobalTargetPoints());
	}

	public void RefreshPhaseDetection(FormTarget target, Vector2[] targetPoints)
	{
		if (target == null || targetPoints == null)
		{
			ResetPhaseDetection();
			return;
		}

		dentro_da_forma = target.IntersectsLeaf(this, targetPoints);
		fora_da_folha = dentro_da_forma
			&& !target.ContainsEntireLeaf(this, targetPoints);
	}

	public void SetControllerSelected(bool selected)
	{
		if (!selected && !mouseControlled && !controllerControlled)
		{
			ZIndex = stackOrder;
		}
	}

	private void BringToFront()
{
	// Salva a ordem antiga antes de ir para o topo
	OldStackOrder = stackOrder; 
	
	// Pula para o limite máximo estabelecido (15)
	stackOrder = 15; 
	ZIndex = stackOrder;

	// Dispara o sinal avisando as outras folhas para se reorganizarem
	if (SignalBus.Instance != null)
	{
		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LeafFocused, this);
	}
}

public void OnAnyLeafFocused(Node2D focusedLeafNode)
{
	// Verifica se o nó focado é uma folha e se NÃO SOU EU mesma
	if (focusedLeafNode is Leaf focusedLeaf && focusedLeaf != this)
	{
		// Se a minha ordem atual estava acima ou igual a ordem antiga da folha 
		// que acabou de subir, eu desço 1 degrau para dar espaço.
		if (this.stackOrder >= focusedLeaf.OldStackOrder)
		{
			// O Mathf.Max garante que a folha nunca fique com ZIndex negativo
			this.stackOrder = Mathf.Max(0, this.stackOrder - 1);
			this.ZIndex = this.stackOrder;
		}
	}
}

	public void ResetPlacementOrder()
	{
		CancelPlacementAnimation();
		//hasBeenPlaced = false;
		stackOrder = initialStackOrder;
		ZIndex = stackOrder;
	}

	// private void RegisterFirstPlacement()
	// {
	// 	if (hasBeenPlaced)
	// 	{
	// 		return;
	// 	}

	// 	hasBeenPlaced = true;
	// 	stackOrder = nextPlacedStackOrder--;
	// } //teste

	private void PlayPlacementAnimation()
	{
		CancelPlacementAnimation();

		if (PlacementAnimationDuration <= 0.0f
			|| PlacementAnimationLift <= 0.0f)
		{
			return;
		}

		float liftDuration = PlacementAnimationDuration * 0.25f;
		float settleDuration = PlacementAnimationDuration - liftDuration;
		Vector2 liftedOffset = baseOffset
			+ new Vector2(0.0f, -PlacementAnimationLift);

		placementTween = CreateTween();
		placementTween.SetTrans(Tween.TransitionType.Sine);
		placementTween.SetEase(Tween.EaseType.Out);
		placementTween.TweenProperty(
			this,
			"offset",
			liftedOffset,
			liftDuration);
		placementTween.SetTrans(Tween.TransitionType.Sine);
		placementTween.SetEase(Tween.EaseType.InOut);
		placementTween.TweenProperty(
			this,
			"offset",
			baseOffset,
			settleDuration);
	}

	private void CancelPlacementAnimation()
	{
		if (placementTween != null)
		{
			placementTween.Kill();
			placementTween = null;
		}

		Offset = baseOffset;
	}

	private void OnLeafHitboxAreaEntered(Area2D area)
	{
		RegisterPhaseArea(area);
		UpdatePhaseFlagsFromPhysics();
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

		UpdatePhaseFlagsFromPhysics();
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

	private void UpdatePhaseFlagsFromPhysics()
	{
		dentro_da_forma = formOverlapCount > 0;
		fora_da_folha = outsideOverlapCount > 0;
	}

	private Vector2[] GetLocalHitboxPoints()
	{
		if (collisionPolygon != null && collisionPolygon.Polygon.Length > 0)
		{
			return collisionPolygon.Polygon;
		}

		if (collisionShape != null && collisionShape.Shape is ConvexPolygonShape2D convexPolygon)
		{
			return convexPolygon.Points;
		}

		if (collisionShape != null && collisionShape.Shape is RectangleShape2D rectangle)
		{
			Vector2 halfSize = rectangle.Size * 0.5f;
			return new Vector2[]
			{
				new(-halfSize.X, -halfSize.Y),
				new(halfSize.X, -halfSize.Y),
				new(halfSize.X, halfSize.Y),
				new(-halfSize.X, halfSize.Y)
			};
		}

		if (Texture != null)
		{
			Vector2 halfSize = Texture.GetSize() * 0.5f;
			return new Vector2[]
			{
				new(-halfSize.X, -halfSize.Y),
				new(halfSize.X, -halfSize.Y),
				new(halfSize.X, halfSize.Y),
				new(-halfSize.X, halfSize.Y)
			};
		}

		return new Vector2[0];
	}

	private bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
	{
		bool inside = false;
		for (int index = 0, previousIndex = polygon.Length - 1;
			index < polygon.Length;
			previousIndex = index++)
		{
			Vector2 current = polygon[index];
			Vector2 previous = polygon[previousIndex];
			if (IsPointOnSegment(point, previous, current))
			{
				return true;
			}

			bool crossesHorizontalLine =
				(current.Y > point.Y) != (previous.Y > point.Y);
			if (!crossesHorizontalLine)
			{
				continue;
			}

			float intersectionX =
			(previous.X - current.X) * (point.Y - current.Y)
			/ (previous.Y - current.Y) + current.X;
			if (point.X < intersectionX)
			{
				inside = !inside;
			}
		}

		return inside;
	}

	private bool IsPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		float cross = (end - start).Cross(point - start);
		if (Mathf.Abs(cross) > 0.5f)
		{
			return false;
		}

		return point.X >= Mathf.Min(start.X, end.X) - 0.5f
			&& point.X <= Mathf.Max(start.X, end.X) + 0.5f
			&& point.Y >= Mathf.Min(start.Y, end.Y) - 0.5f
			&& point.Y <= Mathf.Max(start.Y, end.Y) + 0.5f;
	}

	private void KeepInsideViewport()
	{
		Vector2[] globalPoints = GetGlobalHitboxPoints();
		if (globalPoints.Length == 0)
		{
			return;
		}

		Vector2 min = globalPoints[0];
		Vector2 max = globalPoints[0];
		foreach (Vector2 point in globalPoints)
		{
			min = new Vector2(
				Mathf.Min(min.X, point.X),
				Mathf.Min(min.Y, point.Y));
			max = new Vector2(
				Mathf.Max(max.X, point.X),
				Mathf.Max(max.Y, point.Y));
		}

		Rect2 viewport = GetViewportRect();
		Vector2 correction = Vector2.Zero;
		if (min.X < viewport.Position.X)
		{
			correction.X = viewport.Position.X - min.X;
		}
		else if (max.X > viewport.End.X)
		{
			correction.X = viewport.End.X - max.X;
		}

		if (min.Y < viewport.Position.Y)
		{
			correction.Y = viewport.Position.Y - min.Y;
		}
		else if (max.Y > viewport.End.Y)
		{
			correction.Y = viewport.End.Y - max.Y;
		}

		GlobalPosition += correction;
	}
}
