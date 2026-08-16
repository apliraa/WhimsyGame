using Godot;
using System;

public partial class SceneSwitcher : Node2D
{
	[Export] private ColorRect Rect;
	[Export] private PackedScene NextScene;
	
	private bool IsOpening = true;
	private bool IsClosing = false;
	
	public override void _Ready()
	{
		// PackedScenes não instaciados
		if (NextScene == null)
		{
			GD.PrintErr("NextScene not instantiated!");
		}
		
		CloseScreen();
	}
	
	private void OpenScreen()
	{
		Tween tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 0.0f, 1.0f);
	}
	
	private async void CloseScreen()
	{
		Tween tween = CreateTween();
		tween.TweenProperty(Rect, "color:a", 1.0f, 1.0f);
		
		await ToSignal(tween, Tween.SignalName.Finished);
		
		GetTree().ChangeSceneToPacked(NextScene);
		
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		OpenScreen();
	}
}
