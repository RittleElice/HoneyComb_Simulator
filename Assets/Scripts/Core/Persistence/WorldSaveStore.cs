using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>Two independently checksummed generations. Replace only the older slot; never destroy the newest good file.</summary>
public sealed class WorldSaveStore
{
    private const int MaximumBytes=128*1024*1024;
    public string DirectoryPath { get; }
    public bool HasFiles => File.Exists(Path.Combine(DirectoryPath,"world-a.save")) || File.Exists(Path.Combine(DirectoryPath,"world-b.save"));
    public WorldSaveStore(string directory) { DirectoryPath=directory; }
    private sealed class Generation { public long Sequence; public WorldSaveSnapshot Snapshot; public string Path; }
    public WorldSaveSnapshot Load(out bool recovered)
    {
        var a=Read("world-a.save"); var b=Read("world-b.save");
        var newest=a==null?b:b==null?a:a.Sequence>=b.Sequence?a:b;
        if(newest==null) throw new InvalidDataException("읽을 수 있는 저장 파일이 없습니다. 기존 파일은 보존됩니다.");
        recovered=(a==null&&File.Exists(Path.Combine(DirectoryPath,"world-a.save"))) || (b==null&&File.Exists(Path.Combine(DirectoryPath,"world-b.save")));
        return newest.Snapshot;
    }
    public void Save(byte[] payload)
    {
        if(payload.Length>MaximumBytes-64) throw new InvalidDataException("저장 데이터 크기 한도를 초과했습니다.");
        WorldSaveCodec.Decode(payload); // Reject an unsupported/inconsistent snapshot before replacing either generation.
        Directory.CreateDirectory(DirectoryPath);
        var a=Read("world-a.save"); var b=Read("world-b.save");
        long sequence=checked(Math.Max(a?.Sequence??0,b?.Sequence??0)+1);
        string name=a==null?"world-a.save":b==null?"world-b.save":a.Sequence<=b.Sequence?"world-a.save":"world-b.save";
        string target=Path.Combine(DirectoryPath,name), temp=target+".tmp";
        byte[] body;
        using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream))
        { w.Write(0x48435331); w.Write(sequence); w.Write(payload.Length); w.Write(payload); w.Flush(); body=stream.ToArray(); }
        byte[] hash; using(var sha=SHA256.Create()) hash=sha.ComputeHash(body);
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        { file.Write(body,0,body.Length); file.Write(hash,0,hash.Length); file.Flush(true); }
        if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target);
    }
    private Generation Read(string name)
    {
        string path=Path.Combine(DirectoryPath,name);
        try
        {
            if(!File.Exists(path)) return null;
            long length=new FileInfo(path).Length; if(length<48||length>MaximumBytes) return null;
            byte[] bytes=File.ReadAllBytes(path); int bodyLength=bytes.Length-32;
            byte[] hash; using(var sha=SHA256.Create()) hash=sha.ComputeHash(bytes,0,bodyLength);
            for(int i=0;i<32;i++) if(hash[i]!=bytes[bodyLength+i]) return null;
            using(var stream=new MemoryStream(bytes,0,bodyLength,false)) using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=0x48435331) return null;
                long sequence=r.ReadInt64(); int size=r.ReadInt32(); if(sequence<1||size!=bodyLength-16) return null;
                return new Generation { Sequence=sequence, Snapshot=WorldSaveCodec.Decode(r.ReadBytes(size)), Path=path };
            }
        }
        catch(Exception e) when(e is IOException || e is ArgumentException || e is OverflowException || e is UnauthorizedAccessException)
        { return null; }
    }
}
