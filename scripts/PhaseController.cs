using Godot;
using System.Collections.Generic;

public partial class PhaseController : Node
{
	[Signal]
	public delegate void PhaseAcceptedEventHandler(int phaseIndex);

	[Signal]
	public delegate void AllPhasesAcceptedEventHandler();

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float RequiredCoverage { get; set; } = 0.90f;
	[Export(PropertyHint.Range, "0.02,0.5,0.01")]
	public float AcceptanceCheckInterval { get; set; } = 0.05f;

	private readonly List<Leaf> leaves = new();
	private readonly List<FormTarget> formTargets = new();
	private readonly List<OutsideTrigger> outsideTriggers = new();
	private readonly List<ColorRect> backgrounds = new();
	private int currentPhase = -1;
	private double acceptanceCheckTimer;

	public int CurrentPhase => currentPhase;

	public override void _Ready()
	{
		CollectLeaves();
		CollectFormTargets();
		CollectOutsideTriggers();
		CollectBackgrounds();

		if (formTargets.Count > 0)
		{
			ActivatePhase(0);
		}
	}

	public override void _Process(double delta)
	{
		if (currentPhase < 0 || currentPhase >= formTargets.Count)
		{
			return;
		}

		acceptanceCheckTimer -= delta;
		if (acceptanceCheckTimer > 0.0)
		{
			return;
		}

		acceptanceCheckTimer = AcceptanceCheckInterval;

		FormTarget currentTarget = formTargets[currentPhase];
		foreach (Leaf leaf in leaves)
		{
			leaf.RefreshPhaseDetection();
		}

		float coverage = currentTarget.CalculateCoverage(leaves);
		if (coverage < RequiredCoverage)
		{
			return;
		}

		foreach (Leaf leaf in leaves)
		{
			if (leaf.dentro_da_forma && leaf.fora_da_folha)
			{
				return;
			}
		}

		AcceptCurrentPhase();
	}

	private void CollectLeaves()
	{
		foreach (Node node in GetTree().GetNodesInGroup("leaves"))
		{
			if (node is Leaf leaf)
			{
				leaves.Add(leaf);
			}
		}
	}

	private void CollectFormTargets()
	{
		Node targetsRoot = GetNode<Node>("../FormTargets");
		foreach (Node child in targetsRoot.GetChildren())
		{
			if (child is FormTarget target)
			{
				formTargets.Add(target);
			}
		}
	}

	private void CollectOutsideTriggers()
	{
		Node triggersRoot = GetNode<Node>("../OutsideTriggers");
		foreach (Node child in triggersRoot.GetChildren())
		{
			if (child is OutsideTrigger trigger)
			{
				outsideTriggers.Add(trigger);
			}
		}
	}

	private void CollectBackgrounds()
	{
		Node backgroundsRoot = GetNode<Node>("../Backgrounds");
		foreach (Node child in backgroundsRoot.GetChildren())
		{
			if (child is ColorRect background)
			{
				backgrounds.Add(background);
			}
		}
	}

	private void AcceptCurrentPhase()
	{
		int acceptedPhase = currentPhase;
		GD.Print($"Fase validada: {acceptedPhase + 1}/{formTargets.Count} (cobertura: {formTargets[acceptedPhase].LastCoverage:P1})");
		EmitSignal(SignalName.PhaseAccepted, acceptedPhase);

		int nextPhase = currentPhase + 1;
		if (nextPhase >= formTargets.Count)
		{
			currentPhase = -1;
			GD.Print("Todas as fases foram validadas.");
			EmitSignal(SignalName.AllPhasesAccepted);
			CallDeferred(MethodName.CloseGameAfterCompletion);
			return;
		}

		ActivatePhase(nextPhase);
	}

	private void CloseGameAfterCompletion()
	{
		GetTree().Quit();
	}

	private void ActivatePhase(int phaseIndex)
	{
		currentPhase = phaseIndex;

		for (int index = 0; index < formTargets.Count; index++)
		{
			formTargets[index].SetActive(index == phaseIndex);
		}

		for (int index = 0; index < outsideTriggers.Count; index++)
		{
			outsideTriggers[index].SetActive(false);
		}

		if (phaseIndex < outsideTriggers.Count)
		{
			outsideTriggers[phaseIndex].ConfigureForTarget(formTargets[phaseIndex]);
			outsideTriggers[phaseIndex].SetActive(true);
		}

		for (int index = 0; index < backgrounds.Count; index++)
		{
			backgrounds[index].Visible = index == phaseIndex;
		}

		foreach (Leaf leaf in leaves)
		{
			leaf.ResetPhaseDetection();
		}
	}
}
