using Godot;
using System.Collections.Generic;

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

	[Export] public PackedScene GameWinScene;

	[Export] public DayShader timeCycle;
	private readonly List<Leaf> leaves = new();
	private readonly List<FormTarget> formTargets = new();
	private readonly List<OutsideTrigger> outsideTriggers = new();
	private readonly List<ColorRect> backgrounds = new();
	private int currentPhase = -1;
	private double acceptanceCheckTimer;
	private Timer phaseTransitionTimer;
	private Label debugLabel;
	private TextureRect phaseMessageSprite;
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
		phaseMessageSprite = GetNodeOrNull<TextureRect>("../PhaseSprite");
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
		// Isso é esperado para os assets atuais e não bloqueia a aceitação
		// quando a cobertura foi atingida.
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

		// adicionar caminho
		//var timeCycle = GetNodeOrNull<DayShader>("DayShader/ColorRect");
		if (timeCycle != null)
		{
			timeCycle.SkipTime(0.25f, 2.0f);
		}
		else
		{
			GD.PrintErr("Não encontrou o nó timeCycle. Verifique o caminho!");
		}

		pendingNextPhase = currentPhase + 1;
		currentPhase = -1;
		phaseTransitioning = true;

		if(phaseMessageSprite !=null)
			phaseMessageSprite.Visible = true;

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

		if (phaseMessageSprite != null)
		{
			phaseMessageSprite.Visible = false;
		}

		if (nextPhase >= formTargets.Count)
		{
			currentPhase = -1;
			EmitSignal(SignalName.AllPhasesAccepted);
			CallDeferred(MethodName.GameAfterCompletion);
			return;
		}

		ActivatePhase(nextPhase);
	}

	private void GameAfterCompletion()
	{
		if (GameWinScene != null)
		{
			var switcher = GetNode<SceneSwitcher>("/root/SceneSwitcher");
			
			switcher.TransitionToPacked(GameWinScene);
		}
		else
		{
			GD.PrintErr("A cena de Game Win não foi configurada no PhaseController!");
			GetTree().Quit(); 
		}
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
