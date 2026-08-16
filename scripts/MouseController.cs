using Godot;
using System.Collections.Generic;

public partial class MouseController : Node
{
	private readonly List<Leaf> leaves = new();
	private GamepadCursor gamepadCursor;
	private PhaseController phaseController;
	private Leaf controlledLeaf;
	private Vector2 mouseOffset;
	private Vector2 previousMousePosition;
	private bool waitForMouseRelease;

	public override void _Ready()
	{
		gamepadCursor = GetNode<GamepadCursor>("../Cursor");
		phaseController = GetNodeOrNull<PhaseController>("../PhaseController");
		previousMousePosition = GetViewport().GetMousePosition();

		foreach (Node node in GetTree().GetNodesInGroup("leaves"))
		{
			if (node is Leaf leaf)
			{
				leaves.Add(leaf);
			}
		}
	}

	public override void _Process(double delta)
	{
		Vector2 mousePosition = GetViewport().GetMousePosition();
		bool leftPressed = Input.IsMouseButtonPressed(MouseButton.Left);
		bool rightPressed = Input.IsMouseButtonPressed(MouseButton.Right);

		if (leftPressed || rightPressed || mousePosition.DistanceTo(previousMousePosition) > 0.1f)
		{
			gamepadCursor.Visible = false;
		}

		if (phaseController != null && phaseController.IsTransitioning)
		{
			if (controlledLeaf != null)
			{
				controlledLeaf.EndMouseControl();
				controlledLeaf = null;
			}

			waitForMouseRelease = true;
			previousMousePosition = mousePosition;
			return;
		}

		if (waitForMouseRelease)
		{
			if (!leftPressed && !rightPressed)
			{
				waitForMouseRelease = false;
			}

			previousMousePosition = mousePosition;
			return;
		}

		if (controlledLeaf == null && (leftPressed || rightPressed))
		{
			BeginLeafControl(mousePosition);
		}

		if (controlledLeaf == null)
		{
			previousMousePosition = mousePosition;
			return;
		}

		if (!leftPressed && !rightPressed)
		{
			controlledLeaf.EndMouseControl();
			controlledLeaf = null;
			previousMousePosition = mousePosition;
			return;
		}

		if (leftPressed)
		{
			controlledLeaf.MoveWithMouse(mousePosition - mouseOffset);
		}

		if (rightPressed)
		{
			controlledLeaf.RotateWithMouse(delta);
		}

		previousMousePosition = mousePosition;
	}

	private void BeginLeafControl(Vector2 mousePosition)
	{
		Leaf leafUnderCursor = FindLeafUnderCursor(mousePosition);
		if (leafUnderCursor == null)
		{
			return;
		}

		controlledLeaf = leafUnderCursor;
		mouseOffset = mousePosition - controlledLeaf.GlobalPosition;
		controlledLeaf.BeginMouseControl();
	}

	private Leaf FindLeafUnderCursor(Vector2 mousePosition)
	{
		Leaf leafUnderCursor = null;
		int highestZIndex = int.MinValue;

		foreach (Leaf leaf in leaves)
		{
			if (!leaf.ContainsGlobalPoint(mousePosition))
			{
				continue;
			}

			if (leaf.ZIndex >= highestZIndex)
			{
				highestZIndex = leaf.ZIndex;
				leafUnderCursor = leaf;
			}
		}

		return leafUnderCursor;
	}
}
