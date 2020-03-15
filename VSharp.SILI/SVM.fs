namespace VSharp.Interpreter

open System
open VSharp
open VSharp.Core
open System.Collections
open System.Collections.Generic
open System.Reflection
open Logger
open VSharp.Core
open VSharp.Core.API
open VSharp.Interpreter.IL

module public SVM =
    let private  _explorer = new ILInterpreter();
    let private init = Core.API.Configure(_explorer)
    
    let private pathCondition (summary : codeLocationSummary) =
        summary.state.pc |> Seq.map Term
        
    let private prepareAndInvoke (dictionary : IDictionary option) (m : MethodInfo) invoke =
        let methodIdentifier = _explorer.MakeMethodIdentifier(m)
        
        if  methodIdentifier.Equals(null) then
            printLog Warning "WARNING: metadata method for %s not found!" m.Name
            ()
        let summary = invoke methodIdentifier id
        dictionary |> Option.iter (fun dictionary -> dictionary.Add(m, null))
        printfn
            "*********************************************\n%O\n*************************************************\n"
            (Seq.map toString (pathCondition summary) |> String.concat "\n")
        dictionary |> Option.iter (fun dictionary -> dictionary.[m] <- summary)
        summary
//            System.Console.WriteLine("For {0}.{1} got {2}!", m.DeclaringType.Name, m.Name, ControlFlow.ResultToTerm result)

    let private interpretEntryPoint (dictionary : IDictionary) (m : MethodInfo) =
        assert(m.IsStatic)
        prepareAndInvoke (Some dictionary) m _explorer.InterpretEntryPoint |> ignore
        
    
    let private explore (dictionary : IDictionary) (m : MethodInfo) =
        prepareAndInvoke (Some dictionary) m _explorer.Explore |> ignore

    let private exploreType ignoreList whiteList ep dictionary (t : System.Type) =
        let (|||) = Microsoft.FSharp.Core.Operators.(|||)
        let bindingFlags = BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly
        if List.forall (fun (keyword : String) ->
            not(t.AssemblyQualifiedName.Contains(keyword))) ignoreList &&
           (List.isEmpty whiteList || List.exists (fun (keyword : String) -> t.AssemblyQualifiedName.Contains(keyword)) whiteList) && t.IsPublic then
            t.GetMethods(bindingFlags)
            |> FSharp.Collections.Array.iter (fun m -> 
                match m.GetMethodBody() with
                | null -> ()
                | _ -> if m <> ep && not m.IsAbstract then (*try*) explore dictionary m (*with | _ -> ()*))
        
    let private replaceLambdaLines str =
        System.Text.RegularExpressions.Regex.Replace(str, @"@\d+(\+|\-)\d*\[Microsoft.FSharp.Core.Unit\]", "")
    
    let private resultToString (summary : codeLocationSummary) =
        sprintf "%O\nHEAP:\n%s" summary.result (summary.state |> Memory.Dump |> replaceLambdaLines)
    
    let private subtypeConstraint (summary : codeLocationSummary) =
        summary.state.pc |> Seq.map (toString)

    let public ExploreOne (m : MethodInfo) =
        prepareAndInvoke None m _explorer.Explore |> resultToString

    let public ConfigureSolver (solver : ISolver) =
        Core.API.ConfigureSolver solver

    
    let public Run (assembly : Assembly) (ignoreList : List<_>) (whiteList : List<_>) =
        let ignoreList = List.ofSeq ignoreList
        let whiteList = List.ofSeq whiteList
        let dictionary = new Dictionary<MethodInfo, codeLocationSummary>()
        let ep = assembly.EntryPoint
        assembly.GetTypes() |> FSharp.Collections.Array.iter (exploreType ignoreList whiteList ep dictionary)
        if ep <> null then interpretEntryPoint dictionary ep
        System.Linq.Enumerable.ToDictionary(dictionary :> IEnumerable<_>, (fun kvp -> kvp.Key), fun (kvp : KeyValuePair<MethodInfo, codeLocationSummary>) -> resultToString kvp.Value) :> IDictionary<_, _>
    
//    let public RunGenerator (assembly : Assembly) (ignoreList : List<_>) (whiteList : List<_>) =
//        let ignoreList = List.ofSeq ignoreList
//        let whiteList = List.ofSeq whiteList
//        let dictionary = new Dictionary<MethodInfo, codeLocationSummary>()
//        let ep = assembly.EntryPoint
//        assembly.GetTypes() |> FSharp.Collections.Array.iter (exploreType ignoreList whiteList ep dictionary)
//        if ep <> null then interpretEntryPoint dictionary ep
//        System.Linq.Enumerable.ToDictionary(dictionary :> IEnumerable<_>, (fun kvp -> kvp.Key), fun (kvp : KeyValuePair<MethodInfo, codeLocationSummary>) -> pathConditions kvp.Value) :> IDictionary<_, _>
