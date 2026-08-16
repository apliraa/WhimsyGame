using Godot;
using System.Collections.Generic;

public partial class FormTarget : Area2D
{
	[Export] public int CoverageSamplesX { get; set; } = 64;
	[Export] public int CoverageSamplesY { get; set; } = 64;

	private CollisionShape2D collisionShape;

	public bool IsActive { get; private set; }
	public float LastCoverage { get; private set; }

	public override void _Ready()
	{
		collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		SetActive(Visible);
	}

	public void SetActive(bool active)
	{
		IsActive = active;
		Visible = active;
		Monitoring = active;
		Monitorable = active;

		if (collisionShape != null)
		{
			collisionShape.Disabled = !active;
		}
	}

	public Vector2 GetGlobalTargetSize()
	{
		Vector2[] targetPoints = GetLocalShapePoints();
		if (targetPoints.Length == 0)
		{
			return Vector2.Zero;
		}

		Vector2 min = targetPoints[0];
		Vector2 max = targetPoints[0];
		foreach (Vector2 point in targetPoints)
		{
			min = new Vector2(Mathf.Min(min.X, point.X), Mathf.Min(min.Y, point.Y));
			max = new Vector2(Mathf.Max(max.X, point.X), Mathf.Max(max.Y, point.Y));
		}

		return new Vector2(
			(max.X - min.X) * Mathf.Abs(GlobalScale.X),
			(max.Y - min.Y) * Mathf.Abs(GlobalScale.Y));
	}

	public float CalculateCoverage(IReadOnlyList<Leaf> leaves)
	{
		Vector2[] targetPoints = GetLocalShapePoints();
		if (!IsActive || collisionShape == null || targetPoints.Length == 0)
		{
			LastCoverage = 0.0f;
			return 0.0f;
		}

		int sampleCountX = CoverageSamplesX > 0 ? CoverageSamplesX : 1;
		int sampleCountY = CoverageSamplesY > 0 ? CoverageSamplesY : 1;
		int coveredSamples = 0;
		int totalSamples = 0;
		Vector2 min = targetPoints[0];
		Vector2 max = targetPoints[0];
		foreach (Vector2 point in targetPoints)
		{
			min = new Vector2(Mathf.Min(min.X, point.X), Mathf.Min(min.Y, point.Y));
			max = new Vector2(Mathf.Max(max.X, point.X), Mathf.Max(max.Y, point.Y));
		}

		for (int x = 0; x < sampleCountX; x++)
		{
			float normalizedX = (x + 0.5f) / sampleCountX;
			float localX = Mathf.Lerp(
				min.X,
				max.X,
				normalizedX);

			for (int y = 0; y < sampleCountY; y++)
			{
				float normalizedY = (y + 0.5f) / sampleCountY;
				float localY = Mathf.Lerp(
					min.Y,
					max.Y,
					normalizedY);
				Vector2 localPoint = new Vector2(localX, localY);

				if (!IsPointInsideConvexPolygon(localPoint, targetPoints))
				{
					continue;
				}

				Vector2 globalPoint = collisionShape.ToGlobal(localPoint);
				totalSamples++;
				if (IsPointCoveredByLeaf(globalPoint, leaves))
				{
					coveredSamples++;
				}
			}
		}

		LastCoverage = totalSamples > 0 ? (float)coveredSamples / totalSamples : 0.0f;
		return LastCoverage;
	}

	private Vector2[] GetLocalShapePoints()
	{
		if (collisionShape == null)
		{
			return new Vector2[0];
		}

		if (collisionShape.Shape is ConvexPolygonShape2D convexPolygon)
		{
			return convexPolygon.Points;
		}

		if (collisionShape.Shape is RectangleShape2D rectangle)
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

		return new Vector2[0];
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

	private bool IsPointCoveredByLeaf(Vector2 globalPoint, IReadOnlyList<Leaf> leaves)
	{
		foreach (Leaf leaf in leaves)
		{
			if (leaf.ContainsGlobalPoint(globalPoint))
			{
				return true;
			}
		}

		return false;
	}
}
