using System;
using System.IO;
using UnityEngine;
public static class PyramidMoistureValidation
{
    public static void Run()
    {
        string folder=Directory.GetParent(Application.dataPath).FullName;
        double sensitivity=461.5*293.15/0.0001, sat=610.94*Math.Exp(17.625*20/263.04);
        var mass=new double[15];var slope=new double[15];var temp=new[]{20d,20d,20d};
        var pressure=new double[15];var delta=new double[15];var heat=new double[3];var limit=new double[15];
        var edges=new HiveClimateKernels.Contact[2];
        for(int i=0;i<3;i++){slope[5*i]=sensitivity;mass[5*i]=sat*(i==0?.8:.4)/sensitivity;}
        var fine=(double[])mass.Clone();
        for(int i=0;i<2;i++)edges[i]=new HiveClimateKernels.Contact{a=i*5,b=(i+1)*5,conductance=1e-11,fractionA=1d/6,fractionB=1d/6};
        using(var w=new StreamWriter(Path.Combine(folder,"PyramidChain.csv")))
        {
            w.WriteLine("minute,A_RH,B_RH,C_RH,reference_A,reference_B,reference_C");
            for(int minute=1;minute<=60;minute++){
                for(int i=0;i<3;i++)pressure[i*5]=mass[i*5]*sensitivity;
                Array.Clear(delta,0,15);
                HiveClimateKernels.PairFlux(edges,slope,pressure,mass,limit,delta,heat,temp);
                for(int i=0;i<3;i++)mass[i*5]+=delta[i*5];
                for(int sec=0;sec<60;sec++){
                    double ab=1e-11*sensitivity*(fine[0]-fine[5]),bc=1e-11*sensitivity*(fine[5]-fine[10]);
                    fine[0]-=ab;fine[5]+=ab-bc;fine[10]+=bc;
                }
                double scale=100*sensitivity/sat;
                if(minute==1 && (Math.Abs(mass[10]*scale-40)>1e-10||fine[10]*scale<=40))throw new Exception("Chain propagation check failed");
                if(Math.Abs((mass[0]+mass[5]+mass[10])*scale-160)>1e-8 || Math.Abs(heat[0]+heat[1]+heat[2])>1e-8)throw new Exception("Chain conservation failure");
                w.WriteLine(FormattableString.Invariant($"{minute},{mass[0]*scale:F8},{mass[5]*scale:F8},{mass[10]*scale:F8},{fine[0]*scale:F8},{fine[5]*scale:F8},{fine[10]*scale:F8}"));
            }
        }
        MoisturePressureExperiment.Compare();
        LiveClimateStackTiming.Run();
    }
}
