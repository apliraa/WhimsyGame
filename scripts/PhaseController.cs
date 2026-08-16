using Godot;
using System.Collections.Generic;
using System.Text;

public partial class PhaseController : Node
{
	[Signal]
	public delegate void PhaseAcceptedEventHandler(int phaseIndex);

	[Signal]
	public delegate void AllPhasesAcceptedEventHandler();

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float RequiredCoverage { get; set; } = 0.99f;
	[Export(PropertyHint.Range, "0.02,0.5,0.01")]
	public float AcceptanceCheckInterval { get; set; } = 0.05f;
	[Export] public bool DebugValidation { get; set; } = true;
	[Export(PropertyHint.Range, "0.1,5,0.1")]
	public float DebugLogInterval { get; set; } = 0.5f;

	private readonly List<Leaf> leaves = new();
	private readonly List<FormTarget> formTargets = new();
	private readonly List<OutsideTrigger> outsideTriggers = new();
	private readonly List<ColorRect> backgrounds = new();
	private int currentPhase = -1;
	private double acceptanceCheckTimer;
	private double debugLogTimer;
	private Timer phaseTransitionTimer;
	private Label debugLabel;
	private Label phaseMessage;
	private bool phaseTransitioning;
	private int pendingNextPhase = -1;

	public int CurrentPhase => currentPhase;
	public bool IsTransitioning => phaseTransitioning;

	public override void _Ready()
	{
		CollectLeaves();
		CollectFormTargets();
		CollectOutsideTriggers();
		CollectBackgrounds();
		debugLabel = GetNodeOrNull<Label>("../ValidationDebug");
		phaseMessage = GetNodeOrNull<Label>("../PhaseMessage");
		phaseTransitionTimer = GetNodeOrNull<Timer>("../PhaseTransitionTimer");
		if (phaseTransitionTimer != null)
		{
			phaseTransitionTimer.Timeout += OnPhaseTransitionTimeout;
		}

		if (formTargets.Count > 0)
		{
			ActivatePhase(0);
		}
	}

	public override void _ExitTree()
	{
		if (phaseTransitionTimer != null)
		{
			phaseTransitionTimer.Timeout -= OnPhaseTransitionTimeout;
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
		Vector2[] targetPoints = currentTarget.GetGlobalTargetPoints();
		foreach (Leaf leaf in leaves)
		{
			leaf.RefreshPhaseDetection(currentTarget, targetPoints);
		}

		float coverage = currentTarget.CalculateCoverage(leaves);
		PrintValidationDebug(currentTarget, coverage, delta);
		if (coverage < RequiredCoverage)
		{
			return;
		}

		// Só confirma depois que o jogador soltar a folha. Isso permite
		// pequenos ajustes finais sem a transição bloquear o controle.
		if (IsAnyLeafBeingControlled())
		{
			return;
		}

		// O contorno completo da folha pode ultrapassar o contorno do formato.
		// Isso é esperado para os assets atuais; fora_da_folha fica disponível
		// para debug, mas não bloqueia a aceitação quando a cobertura foi atingida.
		bool hasLeafInsideForm = false;
		foreach (Leaf leaf in leaves)
		{
			if (leaf.dentro_da_forma)
			{
				hasLeafInsideForm = true;
				break;
			}
		}

		if (!hasLeafInsideForm)
		{
			return;
		}

		AcceptCurrentPhase();
	}

	private void PrintValidationDebug(FormTarget target, float coverage, double delta)
	{
		if (!DebugValidation)
		{
			return;
		}

		debugLogTimer -= delta;
		if (debugLogTimer > 0.0)
		{
			return;
		}

		debugLogTimer = DebugLogInterval > 0.0f ? DebugLogInterval : 0.5f;
		int insideCount = 0;
		int outsideCount = 0;
		StringBuilder states = new();

		foreach (Leaf leaf in leaves)
		{
			if (leaf.dentro_da_forma)
			{
				insideCount++;
				if (leaf.fora_da_folha)
				{
					outsideCount++;
				}
			}

			if (states.Length > 0)
			{
				states.Append('\n');
			}

			states.Append(leaf.Name)
				.Append("[dentro=")
				.Append(leaf.dentro_da_forma)
				.Append(", fora=")
				.Append(leaf.fora_da_folha)
				.Append(", pos=")
				.Append(FormatPosition(leaf.GlobalPosition))
				.Append(", bounds=")
				.Append(FormatBounds(leaf.GetGlobalHitboxPoints()))
				.Append(']');
		}

		if (states.Length == 0)
		{
			states.Append("nenhuma folha tocando o formato");
		}

		string message =
			$"[PhaseDebug] fase={currentPhase + 1}/{formTargets.Count} folhas={leaves.Count} "
			+ $"cobertura={coverage:P2} ({target.LastCoveredSamples}/{target.LastTargetSamples}) "
			+ $"necessaria={RequiredCoverage:P0} dentro={insideCount} fora={outsideCount} "
			+ $"alvo={FormatBounds(target.GetGlobalCoveragePoints())}"
			+ $"\n[{states}]";
		GD.Print(message);
		if (debugLabel != null)
		{
			debugLabel.Text = message.Replace(" | ", "\n");
		}
	}

	private string FormatPosition(Vector2 position)
	{
		return $"({Mathf.RoundToInt(position.X)},{Mathf.RoundToInt(position.Y)})";
	}

	private bool IsAnyLeafBeingControlled()
	{
		foreach (Leaf leaf in leaves)
		{
			if (leaf.IsBeingControlled)
			{
				return true;
			}
		}

		return false;
	}

	private string FormatBounds(Vector2[] points)
	{
		if (points == null || points.Length == 0)
		{
			return "vazio";
		}

		Vector2 min = points[0];
		Vector2 max = points[0];
		foreach (Vector2 point in points)
		{
			min = new Vector2(
				Mathf.Min(min.X, point.X),
				Mathf.Min(min.Y, point.Y));
			max = new Vector2(
				Mathf.Max(max.X, point.X),
				Mathf.Max(max.Y, point.Y));
		}

		return $"({Mathf.RoundToInt(min.X)},{Mathf.RoundToInt(min.Y)})-({Mathf.RoundToInt(max.X)},{Mathf.RoundToInt(max.Y)})";
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
		if (phaseTransitioning)
		{
			return;
		}

		int acceptedPhase = currentPhase;
		FormTarget acceptedTarget = formTargets[acceptedPhase];
		GD.Print(
			$"Fase validada: {acceptedPhase + 1}/{formTargets.Count} "
			+ $"(cobertura aceita: {acceptedTarget.LastCoverage:P2}, "
			+ $"mínimo: {RequiredCoverage:P2}, "
			+ $"amostras: {acceptedTarget.LastCoveredSamples}/{acceptedTarget.LastTargetSamples})");
		if (debugLabel != null)
		{
			debugLabel.Text =
				$"[PhaseAccepted] fase={acceptedPhase + 1}/{formTargets.Count} "
				+ $"cobertura aceita={acceptedTarget.LastCoverage:P2} "
				+ $"mínimo={RequiredCoverage:P2}\n"
				+ debugLabel.Text;
		}
		EmitSignal(SignalName.PhaseAccepted, acceptedPhase);

		pendingNextPhase = currentPhase + 1;
		currentPhase = -1;
		phaseTransitioning = true;

		if (phaseMessage != null)
		{
			phaseMessage.Text = pendingNextPhase >= formTargets.Count
				? "Parabéns!\nVocê completou todas as fases!"
				: $"Parabéns! Fase {acceptedPhase + 1} concluída!\nPróxima fase em instantes...";
			phaseMessage.Visible = true;
		}

		if (phaseTransitionTimer != null)
		{
			phaseTransitionTimer.Start();
		}
		else
		{
			OnPhaseTransitionTimeout();
		}
	}

	private void OnPhaseTransitionTimeout()
	{
		if (!phaseTransitioning)
		{
			return;
		}

		phaseTransitioning = false;
		int nextPhase = pendingNextPhase;
		pendingNextPhase = -1;

		if (phaseMessage != null)
		{
			phaseMessage.Visible = false;
		}

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
			leaf.ResetPlacementOrder();
		}
	}
}
