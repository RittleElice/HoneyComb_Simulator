using System;
using UnityEngine;
[CreateAssetMenu(menuName="HoneyComb/Exterior Air Material")]
public sealed class ExteriorAirMaterialAsset:ScriptableObject {
 [Min(.000001f)] public double ConductivityWPerMK=.0254;
 [Min(.000001f)] public double HeatTransferMultiplier=4;
 [Min(.000001f)] public double ThicknessM=.015;
 [Min(0)] public double WindCoefficient=4;
 void OnValidate()=>ThermalMaterials.NotifyAssetsChanged();
 public ExteriorAirValue Read()=>new ExteriorAirValue(ConductivityWPerMK,HeatTransferMultiplier,ThicknessM,WindCoefficient);
}
public readonly struct ExteriorAirValue {
 public readonly double Conductivity,Multiplier,Thickness,WindCoefficient;
 public double EffectiveConductivity=>Conductivity*Multiplier;
 public ExteriorAirValue(double conductivity,double multiplier,double thickness,double windCoefficient){
  if(!ThermalExchange.Finite(conductivity)||conductivity<=0||!ThermalExchange.Finite(multiplier)||multiplier<=0||!ThermalExchange.Finite(thickness)||thickness<=0||!ThermalExchange.Finite(windCoefficient)||windCoefficient<0)throw new ArgumentException("Invalid exterior air material.");
  Conductivity=conductivity;Multiplier=multiplier;Thickness=thickness;WindCoefficient=windCoefficient;
 }
 public double EffectiveThickness(double wind){if(!ThermalExchange.Finite(wind)||wind<0)throw new ArgumentOutOfRangeException(nameof(wind));return Thickness/(1+WindCoefficient*wind*Thickness/EffectiveConductivity);}
 public double Conductance(double area,double internalResistance,double wind)=>area/(internalResistance+EffectiveThickness(wind)/EffectiveConductivity);
 public static ExteriorAirValue Default=>new ExteriorAirValue(.0254,4,.015,4);
}
