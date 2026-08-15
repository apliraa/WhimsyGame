using Godot;
using System;

public partial class OptionsMenu : Control
{
    [Export] public Button Botão1;
    [Export] public Button Botão2;
    [Export] public Button Back;
    [Export] public AnimationPlayer Anim;
    public override void _Ready()
    {
        if (Botão1 != null) Botão1.Pressed += OnBotão1Pressed;
        if (Botão2 != null) Botão2.Pressed += OnBotão2Pressed;
        if (Back != null) Back.Pressed += OnBackPressed;
        
    }

    public void OnBotão1Pressed()
    {
        
    }

    public void OnBotão2Pressed()
    {
        
    }

    public void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MenuUI.tscn");
    }


}
