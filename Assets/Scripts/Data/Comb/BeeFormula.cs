using System;
using System.Collections.Generic;
using System.Globalization;

// Small arithmetic language, not C# execution. Expressions are editable in the settings asset.
public sealed class BeeFormula
{
    delegate double Node(Func<string,double> variables);
    readonly Node root;
    readonly HashSet<string> names = new HashSet<string>();
    string source; int at;
    public IEnumerable<string> Variables => names;
    readonly Func<string,Func<double>> bind;
    public BeeFormula(string expression,Func<string,Func<double>> bind=null)
    {
        this.bind=bind;
        source=expression??throw new ArgumentNullException(nameof(expression));
        if(source.Length>2048)throw new ArgumentException("Formula exceeds 2048 characters.");
        root=Sum(); White(); if(at!=source.Length)throw new ArgumentException("Unexpected formula token at "+at);
    }
    void White(){while(at<source.Length&&char.IsWhiteSpace(source[at]))at++;}
    bool Eat(char c){White();if(at<source.Length&&source[at]==c){at++;return true;}return false;}
    Node Sum(){var a=Product();while(true){if(Eat('+')){var l=a;var r=Product();a=v=>l(v)+r(v);}else if(Eat('-')){var l=a;var r=Product();a=v=>l(v)-r(v);}else return a;}}
    Node Product(){var a=Atom();while(true){if(Eat('*')){var l=a;var r=Atom();a=v=>l(v)*r(v);}else if(Eat('/')){var l=a;var r=Atom();a=v=>l(v)/r(v);}else return a;}}
    int depth;
    Node Atom()
    {
        if(++depth>64)throw new ArgumentException("Formula nesting limit.");
        try
        {
            if(Eat('-')){var n=Atom();return v=>-n(v);} if(Eat('+'))return Atom();
            if(Eat('(')){var n=Sum();if(!Eat(')'))throw new ArgumentException("Missing ).");return n;}
            White();int start=at;
            if(at<source.Length&&(char.IsDigit(source[at])||source[at]=='.'))
            {
                while(at<source.Length&&(char.IsDigit(source[at])||source[at]=='.'))at++;
                if(at<source.Length&&(source[at]=='e'||source[at]=='E')){at++;if(at<source.Length&&(source[at]=='+'||source[at]=='-'))at++;while(at<source.Length&&char.IsDigit(source[at]))at++;}
                double value=double.Parse(source.Substring(start,at-start),CultureInfo.InvariantCulture);return v=>value;
            }
            while(at<source.Length&&(char.IsLetterOrDigit(source[at])||source[at]=='_'))at++;
            if(start==at)throw new ArgumentException("Expected number/variable at "+at);
            string name=source.Substring(start,at-start);
            if(!Eat('(')){names.Add(name);var bound=bind?.Invoke(name);return bound==null?(Node)(v=>v(name)):(v=>bound());}
            var args=new List<Node>();if(!Eat(')')){do{args.Add(Sum());}while(Eat(','));if(!Eat(')'))throw new ArgumentException("Missing function ).");}
            int arity=name=="abs"||name=="sqrt"||name=="exp"||name=="log"?1:name=="min"||name=="max"||name=="pow"?2:name=="clamp"?3:name=="band"?6:-1;
            if(args.Count!=arity)throw new ArgumentException("Unknown function or wrong arguments: "+name);
            return v=>{
                double a=args[0](v);
                switch(name){case "abs":return Math.Abs(a);case "sqrt":return Math.Sqrt(a);case "exp":return Math.Exp(a);case "log":return Math.Log(a);
                    case "min":return Math.Min(a,args[1](v));case "max":return Math.Max(a,args[1](v));case "pow":return Math.Pow(a,args[1](v));
                    case "clamp":return Math.Max(args[1](v),Math.Min(args[2](v),a));
                    case "band":return a<args[1](v)?args[3](v):a<args[2](v)?args[4](v):args[5](v);default:throw new ArgumentException(name);}
            };
        }
        finally{depth--;}
    }
    public double Evaluate(Func<string,double> variables)
    {
        double value=root(variables);if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("Formula result is not finite: "+source);return value;
    }
}
