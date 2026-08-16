using Godot;
using System;

public partial class SceneSwitcher : CanvasLayer 
{
	[Export] private ColorRect Rect;
	
	private Tween tween;
	private bool isTransitioning = false; 
	
	public override void _Ready()
	{
		Color corInicial = Rect.Color;
		corInicial.A = 0.0f; 
		Rect.Color = corInicial;

		Rect.MouseFilter = Control.MouseFilterEnum.Ignore;
	}
	private void OpenScreen(float time = 1.0f)
	{
		
		Rect.MouseFilter = Control.MouseFilterEnum.Ignore;
		
		tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 0.0f, time);
	}
	
	private void CloseScreen(float time = 1.0f)
	{
	
		Rect.MouseFilter = Control.MouseFilterEnum.Stop;
		
		tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 1.0f, time);
	}
	
	public async void TransitionToPacked(PackedScene scene)
	{
	
		if (isTransitioning) return;
		isTransitioning = true;
		
		CloseScreen();
		
		await ToSignal(tween, Tween.SignalName.Finished);
		GetTree().ChangeSceneToPacked(scene);
		
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		OpenScreen();
		
		await ToSignal(tween, Tween.SignalName.Finished);
		isTransitioning = false;
	}
}
