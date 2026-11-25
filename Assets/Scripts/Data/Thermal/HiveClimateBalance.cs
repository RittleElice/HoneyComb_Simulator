using System;
using System.Linq;
public static partial class HiveClimate {
public sealed partial class Model {
ClimateBalanceSolver waterBalance,heatBalance;int[] waterSlots,waterMap;
double[] nextMass,nextEnergy,waterStart,waterPressure,waterTrial,waterExterior,heatExterior;
void EnsureBalances(){if(waterBalance!=null)return;waterSlots=contacts.SelectMany(c=>new[]{c.a,c.b}).Concat(exteriorContacts.Select(c=>c.reservoir)).Distinct().OrderBy(p=>p).ToArray();waterMap=Enumerable.Repeat(-1,mass.Length).ToArray();for(int i=0;i<waterSlots.Length;i++)waterMap[waterSlots[i]]=i;
waterBalance=new ClimateBalanceSolver(waterSlots.Length,contacts.Select(c=>waterMap[c.a]).ToArray(),contacts.Select(c=>waterMap[c.b]).ToArray(),contacts.Select(c=>c.conductance).ToArray(),60);
heatBalance=new ClimateBalanceSolver(n,links.Select(f=>f.a).ToArray(),links.Select(f=>f.b).ToArray(),links.Select(f=>f.heat).ToArray(),60);
nextMass=new double[mass.Length];nextEnergy=new double[n];waterStart=new double[waterSlots.Length];waterPressure=new double[waterSlots.Length];waterTrial=new double[waterSlots.Length];waterExterior=new double[exteriorContacts.Length];heatExterior=new double[outsideHeatContacts?.Length??0];}
double WaterMass(int slot,double vapor,out double derivative){int row=slot/Kinds,kind=slot%Kinds;double sat=Psat(temperature[row]);if(kind==Air){double v=contentFreeVolume[row]-nextMass[row*Kinds+Nectar]/waterDensity;derivative=v/(Rv*(273.15+temperature[row]));return vapor*derivative;}if(kind!=Wood){derivative=limit[slot]/sat;return vapor*derivative;}
double rh=vapor/sat;double ratio=HiveClimateKernels.WoodMoistureRatio(rh);double epsilon=1e-7;derivative=limit[slot]/HiveClimateKernels.WoodCapacityRatio*(HiveClimateKernels.WoodMoistureRatio(rh+epsilon)-HiveClimateKernels.WoodMoistureRatio(rh-epsilon))/(2*epsilon*sat);return limit[slot]/HiveClimateKernels.WoodCapacityRatio*ratio;}
double WaterResidual(double[] p){var wb=waterBalance;for(int i=0;i<waterSlots.Length;i++)wb.Rhs[i]=WaterMass(waterSlots[i],p[i],out _)-waterStart[i];for(int j=0;j<contacts.Length;j++){int a=wb.A[j],b=wb.B[j];double dm=60*wb.G[j]*(p[a]-p[b]);wb.Rhs[a]+=dm;wb.Rhs[b]-=dm;}for(int j=0;j<waterExterior.Length;j++){int i=waterMap[exteriorContacts[j].reservoir];wb.Rhs[i]+=60*waterExterior[j]*(p[i]-outsideVaporPressure);}double e=0;for(int i=0;i<waterSlots.Length;i++)e+=Math.Abs(wb.Rhs[i]);return e;}
void AccumulateBalance(){EnsureBalances();for(int p=0;p<mass.Length;p++)nextMass[p]=mass[p]+delta[p];Array.Copy(dE,nextEnergy,n);var wb=waterBalance;
for(int i=0;i<waterSlots.Length;i++){int p=waterSlots[i];waterStart[i]=nextMass[p];double sat=Psat(temperature[p/Kinds]);waterPressure[i]=p%Kinds==Air?nextMass[p]*Rv*(273.15+temperature[p/Kinds])/(contentFreeVolume[p/Kinds]-nextMass[p/Kinds*Kinds+Nectar]/waterDensity):p%Kinds==Wood?sat*HiveClimateKernels.WoodActivity(nextMass[p],limit[p]):sat*nextMass[p]/limit[p];}
Array.Clear(waterExterior,0,waterExterior.Length);if(exteriorMoistureEnabled)for(int j=0;j<waterExterior.Length;j++){var c=exteriorContacts[j];waterExterior[j]=HiveClimateKernels.ExteriorMoistureConductance(c,c.reservoir%Kinds,outsideWind,outdoors,exteriorTransfers.MoistureWindCoefficient,exteriorTransfers.MoistureAirPermeability,exteriorTransfers.MoistureExteriorAirFilmM);}
bool converged=waterSlots.Length==0;double total=waterStart.Sum();
for(int iteration=0;iteration<exteriorTransfers.BalanceNonlinearIterations&&!converged;iteration++){
for(int i=0;i<waterSlots.Length;i++){double m=WaterMass(waterSlots[i],waterPressure[i],out double slope);wb.Base[i]=slope;wb.Rhs[i]=waterStart[i]-m+slope*waterPressure[i];wb.X[i]=waterPressure[i];}
for(int j=0;j<waterExterior.Length;j++){int i=waterMap[exteriorContacts[j].reservoir];double g=60*waterExterior[j];wb.Base[i]+=g;wb.Rhs[i]+=g*outsideVaporPressure;}
wb.Solve(exteriorTransfers.BalanceRelativeTolerance,exteriorTransfers.BalanceLinearIterations);
double tolerance=Math.Max(exteriorTransfers.BalanceMassAbsoluteToleranceKg,total*exteriorTransfers.BalanceMassTolerance);
double previousError=WaterResidual(waterPressure),damping=1,error=double.PositiveInfinity;
for(int backtrack=0;backtrack<40;backtrack++){
    bool positive=true;for(int i=0;i<waterSlots.Length;i++){waterTrial[i]=waterPressure[i]+damping*(wb.X[i]-waterPressure[i]);positive &= waterTrial[i]>=0;}
    if(positive){error=WaterResidual(waterTrial);if(error<previousError || error<=tolerance)break;}
    damping*=.5;
}
if(!ThermalExchange.Finite(error)||error>previousError&&error>tolerance)throw new InvalidOperationException("Water balance line search failed");
Array.Copy(waterTrial,waterPressure,waterPressure.Length);converged=error<=tolerance;

}
if(!converged)throw new InvalidOperationException("Nonlinear water balance did not converge: residual="+WaterResidual(waterPressure)+" threshold="+Math.Max(exteriorTransfers.BalanceMassAbsoluteToleranceKg,total*exteriorTransfers.BalanceMassTolerance)+" slots="+waterSlots.Length);
// Commit water by signed fluxes, not by replacing it with an approximate nonlinear iterate.
for(int j=0;j<contacts.Length;j++){var c=contacts[j];double dm=60*c.conductance*(waterPressure[waterMap[c.a]]-waterPressure[waterMap[c.b]]);nextMass[c.a]-=dm;nextMass[c.b]+=dm;int donor=dm>=0?c.a:c.b;double enthalpy=dm*(Latent+VaporCp*temperature[donor/Kinds]);nextEnergy[c.a/Kinds]-=enthalpy;nextEnergy[c.b/Kinds]+=enthalpy;if(flowTransfers!=null)flowTransfers[j]=dm;}
double externalWater=0,externalVapor=0;for(int j=0;j<waterExterior.Length;j++){int p=exteriorContacts[j].reservoir;double dm=60*waterExterior[j]*(outsideVaporPressure-waterPressure[waterMap[p]]);nextMass[p]+=dm;double h=dm*(Latent+VaporCp*(dm>=0?outdoors:temperature[p/Kinds]));nextEnergy[p/Kinds]+=h;externalWater+=dm;externalVapor+=h;if(flowExterior!=null)flowExterior[j]=dm;}
foreach(double m in nextMass)if(m<0||!ThermalExchange.Finite(m))throw new InvalidOperationException("Invalid water balance mass");
var hb=heatBalance;for(int row=0;row<n;row++){double v=contentFreeVolume[row]-nextMass[row*Kinds+Nectar]/waterDensity;double c=dry[row]+v*airCv,phase=0;for(int k=0;k<Kinds;k++){c+=nextMass[row*Kinds+k]*Cp(k);phase+=nextMass[row*Kinds+k]*Offset[k];}hb.Base[row]=c;hb.Rhs[row]=energy[row]+nextEnergy[row]-phase;hb.X[row]=temperature[row];}
Array.Clear(heatExterior,0,heatExterior.Length);if(exteriorMoistureEnabled)for(int j=0;j<heatExterior.Length;j++){var c=outsideHeatContacts[j];double g=c.baseConductance+c.area*exteriorTransfers.HeatWindCoefficient*outsideWind;heatExterior[j]=g;hb.Base[c.row]+=60*g;hb.Rhs[c.row]+=60*g*outdoors;}
hb.Solve(exteriorTransfers.BalanceRelativeTolerance,exteriorTransfers.BalanceLinearIterations);
for(int j=0;j<links.Length;j++){var f=links[j];double q=60*f.heat*(hb.X[f.a]-hb.X[f.b]);nextEnergy[f.a]-=q;nextEnergy[f.b]+=q;}
double externalHeat=0;for(int j=0;j<heatExterior.Length;j++){var c=outsideHeatContacts[j];double q=60*heatExterior[j]*(outdoors-hb.X[c.row]);nextEnergy[c.row]+=q;externalHeat+=q;}
for(int row=0;row<n;row++)if(!ThermalExchange.Finite(hb.X[row])||hb.X[row]<=-273.15)throw new InvalidOperationException("Invalid heat balance temperature");
Array.Copy(nextMass,mass,mass.Length);for(int row=0;row<n;row++){energy[row]+=nextEnergy[row];dE[row]=0;}Array.Clear(delta,0,delta.Length);ExternalWaterKg=externalWater;ExternalMoistureEnergyJ=externalVapor;ExternalSensibleEnergyJ=externalHeat;stateReady=false;Prepare(pressures:false);
}
}
}
