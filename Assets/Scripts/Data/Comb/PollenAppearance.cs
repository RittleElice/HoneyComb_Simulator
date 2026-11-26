using System;
using System.IO;

public sealed partial class FrameSurface
{
    private uint[] pollenSeeds;
    // Independent visual random stream: never consumes simulation or Unity random values.
    private uint pollenRandom;
    public uint GetPollenAppearanceSeed(int x, int y) => pollenSeeds?[GetIndex(x,y)] ?? 0;
    private void SyncPollenSeed(int index)
    {
        bool present = cells[index].Content == CellContentKind.Pollen &&
            ((materials != null && materials.Has(index)) ? materials.Mass(index) > 0 : cells[index].FillRatio > 0);
        if (!present) { if (pollenSeeds != null) pollenSeeds[index] = 0; return; }
        if (pollenSeeds == null) pollenSeeds = new uint[cells.Length];
        if (pollenSeeds[index] != 0) return;
        if (pollenRandom == 0) { pollenRandom = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0); if(pollenRandom==0)pollenRandom=1; }
        pollenRandom ^= pollenRandom << 13; pollenRandom ^= pollenRandom >> 17; pollenRandom ^= pollenRandom << 5;
        pollenSeeds[index] = pollenRandom;
    }
    private void WritePollenAppearance(BinaryWriter w)
    {
        w.Write(pollenRandom);
        int count=0; if(pollenSeeds!=null)foreach(uint seed in pollenSeeds)if(seed!=0)count++;
        w.Write(count);
        if(pollenSeeds!=null)for(int i=0;i<pollenSeeds.Length;i++)if(pollenSeeds[i]!=0){w.Write(i);w.Write(pollenSeeds[i]);}
    }
    private void ReadPollenAppearance(BinaryReader r, bool included)
    {
        pollenSeeds=null; pollenRandom=0;
        if(included)
        {
            pollenRandom=r.ReadUInt32(); int count=SaveBinary.Count(r,cells.Length);
            if(count>0)pollenSeeds=new uint[cells.Length];
            for(int n=0;n<count;n++)
            {
                int i=r.ReadInt32(); uint seed=r.ReadUInt32();
                if(i<0||i>=cells.Length||seed==0||pollenSeeds[i]!=0||cells[i].Content!=CellContentKind.Pollen)
                    throw new InvalidDataException("Invalid pollen appearance seed.");
                pollenSeeds[i]=seed;
            }
            for(int i=0;i<cells.Length;i++)
                if(cells[i].Content==CellContentKind.Pollen && (pollenSeeds==null||pollenSeeds[i]==0))
                    throw new InvalidDataException("Missing pollen appearance seed.");
        }
        else for(int i=0;i<cells.Length;i++)SyncPollenSeed(i);
    }
}
