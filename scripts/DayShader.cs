using Godot;
using System;

public partial class DayShader : ColorRect
{
	private float dayTime = 0.0f; 
	private ShaderMaterial material;
	private Tween tweenTempo;

	public override void _Ready()
	{
		material = Material as ShaderMaterial;
		
	   
		UpdateShader(dayTime) ;
	}

   
	public void SkipTime(float skipSize, float timeDuration = 3.0f)
	{
		if (material == null) return;

		// se o jogador passar duas fases muito rápido cancela a animação antiga
		if (tweenTempo != null && tweenTempo.IsRunning())
		{
			tweenTempo.Kill();
		}

		float newTime = dayTime + skipSize;

		tweenTempo = CreateTween();
		
		tweenTempo.TweenMethod(Callable.From<float>(UpdateShader), dayTime, newTime, timeDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		
	 dayTime = newTime;
	}

	//se o valor for maior que 1, ele volta para 0 e adiciona a diferença
	//então 1.25 é o mesmo que 0.25
	private void UpdateShader(float skipTimeValue)
	{
		
		float timeLoop = skipTimeValue % 1.0f;
		
		material.SetShaderParameter("time_of_day", timeLoop);

		
		// float distanceToNoon = Mathf.Abs(timeLoop - 0.5f) * 2f; 
		// float saturation = Mathf.Lerp(1.0f, 0.6f, distanceToNoon); 
		// material.SetShaderParameter("saturation", saturation);
	}
}
