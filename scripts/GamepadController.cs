using Godot;
using System.Collections.Generic;

public partial class GamepadController : Node
{
	[Export] public int ControllerDeviceId { get; set; } = 0;
	[Export] public float StickDeadzone { get; set; } = 0.2f;

	private readonly List<Leaf> leaves = new();
	private GamepadCursor cursor;
	private Leaf controlledLeaf;

	public override void _Ready()
	{
		cursor = GetNode<GamepadCursor>("Cursor");

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
		bool interactPressed = Input.IsJoyButtonPressed(ControllerDeviceId, JoyButton.A);

		Vector2 moveInput = new(
			Input.GetJoyAxis(ControllerDeviceId, JoyAxis.LeftX),
			Input.GetJoyAxis(ControllerDeviceId, JoyAxis.LeftY));
		moveInput = ApplyDeadzone(moveInput);

		if (controlledLeaf == null)
		{
			if (interactPressed)
			{
				BeginLeafControl();
			}

			if (controlledLeaf == null)
			{
				cursor.MoveWithController(moveInput, delta);
				return;
			}
		}

		if (!interactPressed)
		{
			controlledLeaf.EndControllerControl();
			controlledLeaf.SetControllerSelected(false);
			controlledLeaf = null;
			cursor.MoveWithController(moveInput, delta);
			return;
		}

		float leftTrigger = ReadTrigger(JoyAxis.TriggerLeft);
		float rightTrigger = ReadTrigger(JoyAxis.TriggerRight);
		float rotationInput = ApplyDeadzone(rightTrigger - leftTrigger);

		controlledLeaf.MoveWithController(moveInput, delta);
		controlledLeaf.RotateWithController(rotationInput, delta);
		cursor.GlobalPosition = controlledLeaf.GlobalPosition;
	}

	private void BeginLeafControl()
	{
		Leaf leafUnderCursor = FindLeafUnderCursor();
		if (leafUnderCursor == null)
		{
			return;
		}

		controlledLeaf = leafUnderCursor;
		controlledLeaf.SetControllerSelected(true);
		controlledLeaf.BeginControllerControl();
	}

	private Leaf FindLeafUnderCursor()
	{
		Leaf leafUnderCursor = null;
		int highestZIndex = int.MinValue;

		foreach (Leaf leaf in leaves)
		{
			if (!leaf.ContainsGlobalPoint(cursor.GlobalPosition))
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

	private Vector2 ApplyDeadzone(Vector2 input)
	{
		float strength = input.Length();
		if (strength <= StickDeadzone)
		{
			return Vector2.Zero;
		}

		float normalizedStrength = Mathf.InverseLerp(StickDeadzone, 1.0f, strength);
		return input.Normalized() * normalizedStrength;
	}

	private float ApplyDeadzone(float input)
	{
		float strength = Mathf.Abs(input);
		if (strength <= StickDeadzone)
		{
			return 0.0f;
		}

		float normalizedStrength = Mathf.InverseLerp(StickDeadzone, 1.0f, strength);
		return Mathf.Sign(input) * normalizedStrength;
	}

	private float ReadTrigger(JoyAxis axis)
	{
		return Mathf.Clamp(Input.GetJoyAxis(ControllerDeviceId, axis), 0.0f, 1.0f);
	}
}
