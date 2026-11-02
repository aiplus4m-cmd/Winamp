using System; using System.Linq; using NAudio.Wave; using RetroAmp.Audio;
class Buf : ISampleProvider {
  readonly float[] d; int p;
  public Buf(float[] d){this.d=d;}
  public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100,2);
  public int Read(float[] b,int o,int c){ int n=Math.Min(c,d.Length-p); Array.Copy(d,p,b,o,n); p+=n; if(n<c) Array.Clear(b,o+n,c-n); return c; }
}
static class T {
  const int SR=44100;
  static float OldLimit(float x){ float a=Math.Abs(x); if(a<=0.85f) return x; float y=0.85f+0.15f*(float)Math.Tanh((a-0.85f)/0.15f); return x<0?-y:y; }
  static float[] Rock(int secs){
    var r=new Random(3); int n=SR*secs; var x=new float[n*2];
    for(int i=0;i<n;i++){ double t=(double)i/SR, beat=t%0.5, half=(t+0.25)%0.5;
      double kick=Math.Sin(2*Math.PI*(45+90*Math.Exp(-beat*35))*beat)*Math.Exp(-beat*7);
      double snare=(t%1.0>0.5? (r.NextDouble()*2-1)*Math.Exp(-((t%1.0)-0.5)*18):0)*0.7;
      double f=t%2<1?82.4:110; double saw=0; for(int h=1;h<12;h++) saw+=Math.Sin(2*Math.PI*f*h*t)/h; double gtr=Math.Tanh(saw*2.5)*0.55;
      double hat=(r.NextDouble()*2-1)*Math.Exp(-(t%0.125)*90)*0.35;
      double v=kick*0.9+snare+gtr+hat; x[2*i]=(float)v; x[2*i+1]=(float)(v*0.95+hat*0.1);
    }
    float pk=x.Max(Math.Abs); for(int i=0;i<x.Length;i++) x[i]=x[i]/pk*0.985f; // "mastered" to -0.1 dBFS
    // heavy master compression: push loudness up then hard-limit to 0.985 like modern rock masters
    for(int i=0;i<x.Length;i++){ x[i]=(float)Math.Tanh(x[i]*2.2)/ (float)Math.Tanh(2.2)*0.985f; }
    return x;
  }
  static float[] RunNew(float[] src,float[] eq,bool auto,float vol){
    var d=new DspChain(new Buf(src)){Volume=vol,AutoGain=auto,EqEnabled=true}; for(int i=0;i<10;i++) d.SetBand(i,eq[i]);
    var o=new float[src.Length]; for(int k=0;k<o.Length;k+=2048) d.Read(o,k,Math.Min(2048,o.Length-k)); return o; }
  static float[] RunOld(float[] src,float[] eq,float pre,float vol,bool clip){
    var bq=new Biquad[20]; for(int i=0;i<20;i++){bq[i]=new Biquad(); bq[i].SetPeaking(SR,EqPresets.Frequencies[i%10],1.25,eq[i%10]);}
    float p=(float)Math.Pow(10,pre/20.0); var o=new float[src.Length];
    for(int i=0;i<src.Length;i+=2){ float l=src[i]*p,r=src[i+1]*p; for(int j=0;j<10;j++) if(eq[j]!=0){l=bq[j].Process(l);r=bq[j+10].Process(r);} l*=vol*vol; r*=vol*vol; o[i]=clip?OldLimit(l):l; o[i+1]=clip?OldLimit(r):r; }
    return o; }
  // distortion added = error between output and the scaled ideal (same chain at tiny volume => perfectly linear)
  static (double peak,double rmsDb,double sdr) Measure(float[] o,float[] ideal){
    int s=SR*2; double num=0,den=0,ss=0,k;
    double a=0,b=0; for(int i=s;i<o.Length;i++){a+=o[i]*ideal[i]; b+=ideal[i]*ideal[i];} k=a/b;
    for(int i=s;i<o.Length;i++){ double e=o[i]-k*ideal[i]; num+=k*ideal[i]*k*ideal[i]; den+=e*e; ss+=o[i]*o[i]; }
    return (o.Skip(s).Max(Math.Abs), 10*Math.Log10(ss/(o.Length-s)), 10*Math.Log10(num/Math.Max(1e-30,den)));
  }
  static int Main(){
    var src=Rock(8);
    int fails=0;
    void Check(string n,float[] o,float[] ideal,double minClean,double maxPeak){
      var m=Measure(o,ideal); bool ok=m.sdr>=minClean && m.peak<=maxPeak;
      if(!ok) fails++;
      Console.WriteLine($"{(ok?"OK  ":"FAIL")} {n,-34} peak {m.peak:0.000}  loudness {m.rmsDb,6:0.0} dBFS  clean-ness {m.sdr,6:0.0} dB (need >= {minClean})");
    }
    Console.WriteLine("Loud rock master at -0.1 dBFS, volume 100%, every preset with AUTO gain:");
    foreach(var p in EqPresets.BuiltIn)
      Check(p.Key, RunNew(src,p.Value,true,1f), RunNew(src,p.Value,true,0.02f), 35, 0.981);
    Console.WriteLine("Old chain for reference (soft clipper, -2 dB preamp):");
    var rock=EqPresets.BuiltIn.First(p=>p.Key=="Rock").Value;
    var mo=Measure(RunOld(src,rock,-2f,1f,true),RunOld(src,rock,-2f,1f,false));
    Console.WriteLine($"     Rock (old)                         clean-ness {mo.sdr,6:0.0} dB");
    // quiet material must pass completely untouched (only the 5 ms look-ahead delay)
    var q=new float[SR*2*3]; for(int i=0;i<q.Length/2;i++){ float v=(float)(0.3*Math.Sin(2*Math.PI*440*i/SR)); q[2*i]=v; q[2*i+1]=v; }
    var outq=RunNew(q,new float[10],true,1f); int delay=(int)(SR*0.005);
    double maxErr=0; for(int i=SR;i<q.Length/2-delay;i++) maxErr=Math.Max(maxErr,Math.Abs(outq[2*(i+delay)]-q[2*i]));
    bool tOk=maxErr<1e-4; if(!tOk) fails++;
    Console.WriteLine($"{(tOk?"OK  ":"FAIL")} quiet signal untouched (max error {maxErr:E1})");
    Console.WriteLine(fails==0?"ALL PASSED":fails+" FAILURE(S)");
    return fails==0?0:1;
  }
}
