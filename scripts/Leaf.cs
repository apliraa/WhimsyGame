using Godot;
using System;

public partial class Leaf : Sprite2D
{
	private bool dragging = false;
	private Vector2 offset = Vector2.Zero;

	public override void _Ready()
	{
	   SignalBus.Instance.LeafFocused += OnAnyLeafFocused;
	}

	public override void _Process(double delta)
	{
		if (dragging)
		{
			//clamp para impedir que as folhas saiam da tela
			Vector2 posicaoDesejada = GetGlobalMousePosition() - offset;

			Vector2 tamanhoTela = GetViewportRect().Size;

			
			posicaoDesejada.X = Mathf.Clamp(posicaoDesejada.X, 0, tamanhoTela.X);
			posicaoDesejada.Y = Mathf.Clamp(posicaoDesejada.Y, 0, tamanhoTela.Y);

			GlobalPosition = posicaoDesejada;
		}
	}
	
	public void _on_leaf_button_button_down(){
		dragging = true;
		offset = GetGlobalMousePosition() - GlobalPosition;
		ZIndex = 5;

		SignalBus.Instance.EmitSignal(SignalBus.SignalName.LeafFocused, this);
	}
	
	public void _on_leaf_button_button_up(){
		dragging = false;
		
	}
	
	public void OnAnyLeafFocused(Node2D focusedLeaf)
	{
		if(focusedLeaf != this)
		{
			ZIndex -= 1;
		}
	}
}
