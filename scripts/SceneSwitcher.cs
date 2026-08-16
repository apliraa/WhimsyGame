using Godot;
using System;

public partial class SceneSwitcher : Node2D
{
	[Export] private ColorRect Rect;
	
	private Tween tween;
	
	private void OpenScreen(float time = 1.0f)
	{
		tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 0.0f, time);
	}
	
	private void CloseScreen(float time = 1.0f)
	{
		tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 1.0f, time);
	}
	
	public async void TransitionToPacked(PackedScene scene)
	{
		CloseScreen();
		
		await ToSignal(tween, Tween.SignalName.Finished);
		GetTree().ChangeSceneToPacked(scene);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		OpenScreen();
	}
}
