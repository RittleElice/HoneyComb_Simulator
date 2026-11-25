using System;using System.Collections.Generic;using System.Reflection;using UnityEditor;using UnityEngine;
public static class BeeClearingTests {
static BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
static void Require(bool value,string message){if(!value)throw new Exception(message);}
[MenuItem("Tools/HoneyComb/검증/Run Clearing Storage Tests")]
public static void Run(){var select=typeof(BeeCycleRuntime).GetMethod("SelectClearingRegion",F);Require(select!=null,"Missing clearing selector");BeeCycleRuntime.Region Pick(params BeeCycleRuntime.Region[] rows)=>(BeeCycleRuntime.Region)select.Invoke(null,new object[]{new List<BeeCycleRuntime.Region>(rows)});
var a=new BeeCycleRuntime.Region{sugar=30,freeSugar=100};var b=new BeeCycleRuntime.Region{sugar=10,freeSugar=100};Require(ReferenceEquals(Pick(a,b),b),"Select transferable least-content region");Require(Pick(a)==null,"A region cannot supply its own relocation capacity");Require(Pick(new BeeCycleRuntime.Region{sugar=10,freeSugar=100},new BeeCycleRuntime.Region{pollen=20,freePollen=100})==null,"Require capacity for the actual resource");var tie=new BeeCycleRuntime.Region{sugar=10,freeSugar=100};Require(ReferenceEquals(Pick(b,tie),b),"Preserve stable tie order");Require(Pick(new BeeCycleRuntime.Region(),new BeeCycleRuntime.Region())==null,"Do not clear empty regions");
var w=new GameWorld(WeatherAssetLoader.Create(42));try{var runtime=w.BeeCycle;var map=(Dictionary<BeeGridPosition,BeeCycleRuntime.Region>)typeof(BeeCycleRuntime).GetField("regionAt",F).GetValue(runtime);var pos=default(BeeGridPosition);map[pos]=a;var bee=new CycleBee{hasStoreExclusion=true,storeExclusion=new CyclePoint(pos)};var refresh=typeof(BeeCycleRuntime).GetMethod("RefreshStoreExclusion",F);a.clearing=true;refresh.Invoke(runtime,new object[]{bee});Require(bee.hasStoreExclusion,"Keep active clearing exclusion");a.clearing=false;refresh.Invoke(runtime,new object[]{bee});Require(!bee.hasStoreExclusion,"Release obsolete clearing exclusion");}finally{w.Thermal?.Dispose();}
Debug.Log("CLEARING STORAGE REGRESSION PASS");}
}

