using System;
using HoneyComb.Simulation.Environment;
using HoneyComb.Simulation.Environment.Weather;
using HoneyComb.Simulation.Time;
using NUnit.Framework;
namespace HoneyComb.Tests
{
 public sealed class WeatherSystemTests
 {
  private static WeatherBaseline MakeBaseline()
  {
   var values=new double[WeatherBaseline.MinutesPerYear*8];
   for(int i=0;i<WeatherBaseline.MinutesPerYear;i++){
    int k=i*8;values[k]=.5;values[k+1]=.5;values[k+3]=.002;values[k+4]=10;
    values[k+5]=.5;values[k+6]=.01;values[k+7]=1;
   }
   var bytes=new byte[values.Length*8];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
   return new WeatherBaseline(bytes);
  }
  [Test] public void SameSeedsAndRestoredStateKeepWeatherIdenticalAcrossYear()
  {
   var b=MakeBaseline();var p=new WeatherParameters();
   var a=new WeatherSimulator(p,b,startMinute:525590);
   var c=new WeatherSimulator(p,b,startMinute:525590);
   for(long i=525590;i<525620;i++){
    if(i==525600)c.Restore(a.Capture());
    var x=a.Step(i);var y=c.Step(i);
    Assert.That(x.TemperatureCelsius,Is.EqualTo(y.TemperatureCelsius));
    Assert.That(x.PrecipitationMm,Is.EqualTo(y.PrecipitationMm));
    Assert.That(x.SoilWaterMm,Is.EqualTo(y.SoilWaterMm));
   }
   Assert.Throws<ArgumentOutOfRangeException>(()=>a.Step(525600));
  }
  [Test] public void EnvironmentUpdatesEachMinuteAndHourlyCallbackDoesNotResetIt()
  {
   var model=new WeatherSimulator(new WeatherParameters(),MakeBaseline(),startMinute:59);
   var env=new EnvironmentSystem(model,59);
   var clock=new SimulationClock(59,5,true);
   clock.Tick+=env.TickMinute;clock.HourTick+=env.TickHour;
   clock.Update(5,100);
   Assert.That(env.WeatherCurrent.Minute,Is.EqualTo(60));
   Assert.That(env.LastUpdatedHour,Is.EqualTo(1));
   Assert.That(env.ProcessedMinuteTicks,Is.EqualTo(1));
   Assert.That(env.Current.TemperatureCelsius,Is.EqualTo((float)env.WeatherCurrent.TemperatureCelsius));
   Assert.That(env.Current.PrecipitationMmPerHour,Is.EqualTo((float)(env.WeatherCurrent.PrecipitationMm*60)));
   Assert.Throws<ArgumentOutOfRangeException>(()=>env.TickMinute(60));
  }
  [Test] public void BaselineAndParameterErrorsFailBeforeSimulation()
  {
   Assert.Throws<ArgumentException>(()=>new WeatherBaseline(new byte[8]));
   var p=new WeatherParameters();p.C50=0;
   Assert.Throws<ArgumentException>(()=>new WeatherSimulator(p,MakeBaseline()));
  }
 }
}
