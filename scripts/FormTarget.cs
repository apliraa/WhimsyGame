using Godot;
using System.Collections.Generic;

public partial class FormTarget : Area2D
{
	[Export] public int CoverageSamplesX { get; set; } = 64;
	[Export] public int CoverageSamplesY { get; set; } = 64;

	private CollisionShape2D collisionShape;
	private CollisionPolygon2D collisionPolygon;
	private Polygon2D coveragePolygon;

	public bool IsActive { get; private set; }
	public float LastCoverage { get; private set; }
	public int LastCoveredSamples { get; private set; }
	public int LastTargetSamples { get; private set; }

	public override void _Ready()
	{
		collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		collisionPolygon = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
		coveragePolygon = GetNodeOrNull<Polygon2D>("CoveragePolygon");
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

		if (collisionPolygon != null)
		{
			collisionPolygon.Disabled = !active;
		}
	}

	public Vector2 GetGlobalTargetSize()
	{
		Vector2[] targetPoints = GetGlobalTargetPoints();
		if (targetPoints.Length == 0)
		{
			return Vector2.Zero;
		}

		GetBounds(targetPoints, out Vector2 min, out Vector2 max);
		return max - min;
	}

	public Vector2[] GetGlobalTargetPoints()
	{
		Vector2[] localPoints = GetLocalShapePoints();
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

	public bool ContainsGlobalPoint(Vector2 globalPoint)
	{
		Vector2[] localPoints = GetLocalShapePoints();
		if (localPoints.Length == 0)
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

	public float CalculateCoverage(IReadOnlyList<Leaf> leaves)
	{
		Vector2[] targetPoints = GetGlobalCoveragePoints();
		if (!IsActive || targetPoints.Length == 0)
		{
			LastCoverage = 0.0f;
			LastCoveredSamples = 0;
			LastTargetSamples = 0;
			return 0.0f;
		}

		int sampleCountX = CoverageSamplesX > 0 ? CoverageSamplesX : 1;
		int sampleCountY = CoverageSamplesY > 0 ? CoverageSamplesY : 1;
		GetBounds(targetPoints, out Vector2 min, out Vector2 max);

		Vector2[][] leafPoints = new Vector2[leaves.Count][];
		Vector2[] leafMins = new Vector2[leaves.Count];
		Vector2[] leafMaxs = new Vector2[leaves.Count];
		bool[] validLeafPoints = new bool[leaves.Count];
		for (int index = 0; index < leaves.Count; index++)
		{
			Leaf leaf = leaves[index];
			if (leaf == null)
			{
				continue;
			}

			Vector2[] points = leaf.GetGlobalHitboxPoints();
			if (points.Length < 3)
			{
				continue;
			}

			leafPoints[index] = points;
			GetBounds(points, out leafMins[index], out leafMaxs[index]);
			validLeafPoints[index] = true;
		}

		int coveredSamples = 0;
		int totalSamples = 0;
		for (int x = 0; x < sampleCountX; x++)
		{
			float normalizedX = (x + 0.5f) / sampleCountX;
			float globalX = Mathf.Lerp(min.X, max.X, normalizedX);

			for (int y = 0; y < sampleCountY; y++)
			{
				float normalizedY = (y + 0.5f) / sampleCountY;
				Vector2 globalPoint = new(
					globalX,
					Mathf.Lerp(min.Y, max.Y, normalizedY));

				if (!IsPointInsidePolygon(globalPoint, targetPoints))
				{
					continue;
				}

				totalSamples++;
				if (IsPointCoveredByCachedLeaf(
					globalPoint,
					leafPoints,
					leafMins,
					leafMaxs,
					validLeafPoints))
				{
					coveredSamples++;
				}
			}
		}

		LastCoverage = totalSamples > 0
			? (float)coveredSamples / totalSamples
			: 0.0f;
		LastCoveredSamples = coveredSamples;
		LastTargetSamples = totalSamples;
		return LastCoverage;
	}

	public Vector2[] GetGlobalCoveragePoints()
	{
		Vector2[] localPoints = GetLocalCoveragePoints();
		if (localPoints.Length == 0)
		{
			return new Vector2[0];
		}

		Node2D geometryNode = coveragePolygon != null
			&& coveragePolygon.Polygon.Length > 0
			? coveragePolygon
			: collisionPolygon;
		if (geometryNode == null)
		{
			geometryNode = collisionShape;
		}

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

	public bool IntersectsLeaf(Leaf leaf)
	{
		return IntersectsLeaf(leaf, GetGlobalTargetPoints());
	}

	public bool IntersectsLeaf(Leaf leaf, Vector2[] targetPoints)
	{
		if (leaf == null)
		{
			return false;
		}

		Vector2[] leafPoints = leaf.GetGlobalHitboxPoints();
		if (targetPoints.Length < 3 || leafPoints.Length < 3)
		{
			return false;
		}

		foreach (Vector2 point in leafPoints)
		{
			if (IsPointInsidePolygon(point, targetPoints))
			{
				return true;
			}
		}

		foreach (Vector2 point in targetPoints)
		{
			if (IsPointInsidePolygon(point, leafPoints))
			{
				return true;
			}
		}

		for (int targetIndex = 0; targetIndex < targetPoints.Length; targetIndex++)
		{
			Vector2 targetStart = targetPoints[targetIndex];
			Vector2 targetEnd = targetPoints[(targetIndex + 1) % targetPoints.Length];

			for (int leafIndex = 0; leafIndex < leafPoints.Length; leafIndex++)
			{
				Vector2 leafStart = leafPoints[leafIndex];
				Vector2 leafEnd = leafPoints[(leafIndex + 1) % leafPoints.Length];
				if (SegmentsIntersect(targetStart, targetEnd, leafStart, leafEnd))
				{
					return true;
				}
			}
		}

		return false;
	}

	public bool ContainsEntireLeaf(Leaf leaf)
	{
		return ContainsEntireLeaf(leaf, GetGlobalTargetPoints());
	}

	public bool ContainsEntireLeaf(Leaf leaf, Vector2[] targetPoints)
	{
		if (leaf == null)
		{
			return false;
		}

		Vector2[] leafPoints = leaf.GetGlobalHitboxPoints();
		if (targetPoints.Length < 3 || leafPoints.Length < 3)
		{
			return false;
		}

		for (int index = 0; index < leafPoints.Length; index++)
		{
			Vector2 start = leafPoints[index];
			Vector2 end = leafPoints[(index + 1) % leafPoints.Length];
			float edgeLength = start.DistanceTo(end);
			int subdivisions = Mathf.Max(2, Mathf.CeilToInt(edgeLength / 4.0f));

			for (int step = 0; step <= subdivisions; step++)
			{
				Vector2 point = start.Lerp(end, (float)step / subdivisions);
				if (!IsPointInsidePolygon(point, targetPoints))
				{
					return false;
				}
			}
		}

		return true;
	}

	private Vector2[] GetLocalShapePoints()
	{
		if (collisionPolygon != null && collisionPolygon.Polygon.Length > 0)
		{
			return collisionPolygon.Polygon;
		}

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

	private Vector2[] GetLocalCoveragePoints()
	{
		if (coveragePolygon != null && coveragePolygon.Polygon.Length > 0)
		{
			return coveragePolygon.Polygon;
		}

		return GetLocalShapePoints();
	}

	private bool IsPointCoveredByCachedLeaf(
		Vector2 globalPoint,
		Vector2[][] leafPoints,
		Vector2[] leafMins,
		Vector2[] leafMaxs,
		bool[] validLeafPoints)
	{
		for (int index = 0; index < leafPoints.Length; index++)
		{
			if (!validLeafPoints[index])
			{
				continue;
			}

			Vector2 min = leafMins[index];
			Vector2 max = leafMaxs[index];
			if (globalPoint.X < min.X || globalPoint.X > max.X
				|| globalPoint.Y < min.Y || globalPoint.Y > max.Y)
			{
				continue;
			}

			if (IsPointInsidePolygon(globalPoint, leafPoints[index]))
			{
				return true;
			}
		}

		return false;
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

	private bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
	{
		float firstOrientation = (firstEnd - firstStart).Cross(secondStart - firstStart);
		float secondOrientation = (firstEnd - firstStart).Cross(secondEnd - firstStart);
		float thirdOrientation = (secondEnd - secondStart).Cross(firstStart - secondStart);
		float fourthOrientation = (secondEnd - secondStart).Cross(firstEnd - secondStart);

		bool oppositeFirst =
			(firstOrientation > 0.5f && secondOrientation < -0.5f)
			|| (firstOrientation < -0.5f && secondOrientation > 0.5f);
		bool oppositeSecond =
			(thirdOrientation > 0.5f && fourthOrientation < -0.5f)
			|| (thirdOrientation < -0.5f && fourthOrientation > 0.5f);

		return (oppositeFirst && oppositeSecond)
			|| IsPointOnSegment(firstStart, secondStart, secondEnd)
			|| IsPointOnSegment(firstEnd, secondStart, secondEnd)
			|| IsPointOnSegment(secondStart, firstStart, firstEnd)
			|| IsPointOnSegment(secondEnd, firstStart, firstEnd);
	}

	private void GetBounds(Vector2[] points, out Vector2 min, out Vector2 max)
	{
		min = points[0];
		max = points[0];
		foreach (Vector2 point in points)
		{
			min = new Vector2(
				Mathf.Min(min.X, point.X),
				Mathf.Min(min.Y, point.Y));
			max = new Vector2(
				Mathf.Max(max.X, point.X),
				Mathf.Max(max.Y, point.Y));
		}
	}
}
