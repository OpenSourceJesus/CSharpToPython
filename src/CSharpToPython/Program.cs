using System;
using System.Linq;
using PyAst = IronPython.Compiler.Ast;

namespace CSharpToPython {
    public class Program
    {
        static string UNREAL_PROJECT_PATH = Environment.CurrentDirectory + "/BareUEProject";
        static string CODE_PATH = UNREAL_PROJECT_PATH + "/Source/BareUEProject";
        static string[] SCREEN_TO_WORLD_POINT_INDICATORS = { "Camera.main.ScreenToWorldPoint(", "GetComponent<Camera>().ScreenToWorldPoint(" };
        const string CURRENT_KEYBOARD_INDICATOR = "Keyboard.current.";
        const string CURRENT_MOUSE_INDICATOR = "Mouse.current.";
        const string CONSTANT_INDICATOR = "const";
        const string POINTER_INDICATOR = "ptr";
        const string INSTANTIATE_INDICATOR = "Instantiate(";
        // const string RESOURCES_INDICATOR = "Resources.";

        public static void Example() {
            var engineWrapper = new EngineWrapper();
            var engine = engineWrapper.Engine;
            var parsedAst = ParsePythonSource(engine, "clr.Reference[System.Int32](p)").Body;
            var result = ConvertAndRunCode(engineWrapper, "int GetInt() { return 1.0; }");
        }

        private static PyAst.PythonAst ParsePythonSource(Microsoft.Scripting.Hosting.ScriptEngine engine, string code) {
            var src = engine.CreateScriptSourceFromString(code);
            var sourceUnit = Microsoft.Scripting.Hosting.Providers.HostingHelpers.GetSourceUnit(src);
            var langContext = Microsoft.Scripting.Hosting.Providers.HostingHelpers.GetLanguageContext(engine);
            var compilerCtxt = new Microsoft.Scripting.Runtime.CompilerContext(
                    sourceUnit,
                    langContext.GetCompilerOptions(),
                    Microsoft.Scripting.ErrorSink.Default);
            var parser = IronPython.Compiler.Parser.CreateParser(
                compilerCtxt,
                (IronPython.Runtime.PythonOptions)langContext.Options
            );
            return parser.ParseFile(false);
        }

        public static object ConvertAndRunExpression(EngineWrapper engine, string csharpCode) {
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(csharpCode);
            return ConvertAndRunCode(engine, csharpAst);
        }
        
        public static object ConvertAndRunStatements(
                EngineWrapper engine,
                string csharpCode,
                string[] requiredImports = null) {
            var wrappedCode = "object WrapperFunc(){\r\n" + csharpCode + "\r\n})";
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseSyntaxTree(wrappedCode).GetRoot();
            return ConvertAndRunCode(engine, csharpAst, requiredImports);
        }
        public static object ConvertAndRunCode(EngineWrapper engine, string csharpCode) {
            string[] lines = csharpCode.Split('\n');
            List<string> outputLines = new List<string>();
            foreach (string line in lines)
            {
                string outputLine = line;
                if (line.Contains("using UnityEngine"))
                    continue;
                else if (Translator.instance.GetType().Name == "UnityToUnreal")
                {
                    // int indexOfResources = 0;
                    // while (indexOfResources != -1)
                    // {
                    //     indexOfResources = outputLine.IndexOf(RESOURCES_INDICATOR);
                    //     if (indexOfResources != -1)
                    //     {
                            
                    //     }
                    // }
                    outputLine = outputLine.Replace("Time.time", "UGameplayStatics." + CONSTANT_INDICATOR + "GetRealTimeSeconds(GetWorld())");
                    outputLine = outputLine.Replace("Time.deltaTime", "UGameplayStatics." + CONSTANT_INDICATOR + "GetWorldDeltaSeconds(GetWorld())");
                    outputLine = outputLine.Replace("Mathf.Sin", "FMath." + CONSTANT_INDICATOR + "Sin");
                    outputLine = outputLine.Replace("Mathf.Cos", "FMath." + CONSTANT_INDICATOR + "Cos");
                    outputLine = outputLine.Replace("Vector2.right", "FVector." + CONSTANT_INDICATOR + "XAxisVector");
                    outputLine = outputLine.Replace("Vector3.forward", "FVector." + CONSTANT_INDICATOR + "YAxisVector");
                    outputLine = outputLine.Replace("Mathf.Atan2", "UKismetMathLibrary." + CONSTANT_INDICATOR + "Atan2");
                    int indexOfInstantiate = outputLine.IndexOf(INSTANTIATE_INDICATOR);
                    if (indexOfInstantiate != -1)
                    {
                        string replaceWith = "SpawnActor(";
                        int indexOfComma = outputLine.IndexOf(',', indexOfInstantiate);
                        string argument1 = outputLine.SubstringStartEnd(indexOfInstantiate + INSTANTIATE_INDICATOR.Length, indexOfComma);
                        replaceWith += argument1;
                        int indexOfComma2 = outputLine.IndexOf(',', indexOfComma + 1);
                        string argument2 = outputLine.SubstringStartEnd(indexOfComma + 1, indexOfComma2);
                        replaceWith += ", " + argument2;
                        int indexOfParenthesis = outputLine.GetIndexOfMatchingParenthesis(indexOfInstantiate + INSTANTIATE_INDICATOR.Length - 1);
                        string argument3 = outputLine.SubstringStartEnd(indexOfComma2 + 1, indexOfParenthesis);
                        replaceWith += ", " + argument3 + ')';
                        outputLine = outputLine.Replace(outputLine.SubstringStartEnd(indexOfInstantiate, indexOfParenthesis), replaceWith);
                    }
                    int indexOfPosition = outputLine.IndexOf("transform.position");
                    if (indexOfPosition != -1)
                    {
                        int indexOfEquals = outputLine.IndexOf('=', indexOfPosition);
                        if (indexOfEquals != -1)
                        {
                            int indexofSemicolon = outputLine.IndexOf(';', indexOfEquals);
                            string position = outputLine.SubstringStartEnd(indexOfEquals + 1, indexofSemicolon);
                            outputLine = "TeleportTo(" + position + ", GetActorRotation(), true, true);";
                        }
                    }
                    outputLine = outputLine.Replace("transform.position", "GetActorLocation()");
                    outputLine = outputLine.Replace("transform.rotation", "GetActorRotation()");
                    outputLine = outputLine.Replace("transform.up", "GetActorRightVector()");
                    outputLine = outputLine.Replace("Vector2.zero", "FVector." + CONSTANT_INDICATOR + "ZeroVector");
                    outputLine = outputLine.Replace(".x", ".X");
                    outputLine = outputLine.Replace(".y", ".Z");
                    outputLine = outputLine.Replace(".z", ".Y");
                    outputLine = outputLine.Replace("Vector2", "FVector2D");
                    outputLine = outputLine.Replace("Vector3", "FVector");
                    int indexOfCurrentKeyboard = 0;
                    while (indexOfCurrentKeyboard != -1)
                    {
                        indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR);
                        if (indexOfCurrentKeyboard != -1)
                        {
                            int indexOfPeriod = outputLine.IndexOf('.', indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                            string key = outputLine.SubstringStartEnd(indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length, indexOfPeriod);
                            string newKey = "";
                            if (key.EndsWith("Key"))
                            {
                                newKey = key.Replace("Key", "");
                                newKey = newKey.ToUpper();
                            }
                            int indexOfEndOfClauseAfterKey = outputLine.IndexOfAny(new char[] { '.', ' ', ';', ')' }, indexOfPeriod + 1);
                            string clauseAfterKey = outputLine.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterKey);
                            if (clauseAfterKey == "isPressed")
                                outputLine = outputLine.Replace(CURRENT_KEYBOARD_INDICATOR + key + '.' + clauseAfterKey, "UGameplayStatics." + CONSTANT_INDICATOR + "GetPlayerController(GetWorld(), 0)." + POINTER_INDICATOR + "IsInputKeyDown(EKeys." + CONSTANT_INDICATOR + newKey + ")");
                        }
                    }
                    int indexOfCurrentMouse = 0;
                    while (indexOfCurrentMouse != -1)
                    {
                        indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR);
                        if (indexOfCurrentMouse != -1)
                        {
                            string command = outputLine.Substring(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                            if (command.StartsWith("position.ReadValue()"))
                                outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + "position.ReadValue()", "Utils." + CONSTANT_INDICATOR + "GetMousePosition(GetWorld())");
                            else
                            {
                                int indexOfPeriod = outputLine.IndexOf('.', indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                                string button = outputLine.SubstringStartEnd(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length, indexOfPeriod);
                                string key = "";
                                if (button == "leftButton")
                                    key = "LeftMouseButton";
                                else if (button == "rightButton")
                                    key = "RightMouseButton";
                                int indexOfEndOfClauseAfterButton = outputLine.IndexOfAny(new char[] { '.', ' ', ';', ')' }, indexOfPeriod + 1);
                                string clauseAfterButton = outputLine.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterButton);
                                if (clauseAfterButton == "isPressed")
                                    outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + button + '.' + clauseAfterButton, "UGameplayStatics." + CONSTANT_INDICATOR + "GetPlayerController(GetWorld(), 0)." + POINTER_INDICATOR + "IsInputKeyDown(EKeys." + CONSTANT_INDICATOR + key + ")");
                            }
                        }
                    }
                    foreach (string screenToWorldPoiIndicator in SCREEN_TO_WORLD_POINT_INDICATORS)
                    {
                        int screenToWorldPoiIndictorIndex = outputLine.IndexOf(screenToWorldPoiIndicator);
                        if (screenToWorldPoiIndictorIndex != -1)
                            outputLine = outputLine.Replace(screenToWorldPoiIndicator, "Utils." + CONSTANT_INDICATOR + "ScreenToWorldPoint(GetWorld(), ");
                    }
                    int indexOfTrsUp = outputLine.IndexOf("transform.up");
                    if (indexOfTrsUp != -1)
                    {
                        int indexOfStatementEnd = outputLine.IndexOf(';', indexOfTrsUp);
                        string statement = outputLine.SubstringStartEnd(indexOfTrsUp, indexOfStatementEnd);
                        int indexOfEquals = outputLine.IndexOf('=', indexOfTrsUp);
                        string facingText = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfStatementEnd);
                        string rotatorText = "UKismetMathLibrary." + CONSTANT_INDICATOR + "MakeRotFromZ(" + facingText + ")";
                        outputLine = outputLine.Replace(statement, "SetActorRotation(" + rotatorText + ", ETeleportType." + CONSTANT_INDICATOR + "TeleportPhysics);");
                    }
                    int indexOfTrsEulerAngles = outputLine.IndexOf("transform.eulerAngles");
                    if (indexOfTrsEulerAngles != -1)
                    {
                        int indexOfStatementEnd = outputLine.IndexOf(';', indexOfTrsEulerAngles);
                        string statement = outputLine.SubstringStartEnd(indexOfTrsEulerAngles, indexOfStatementEnd);
                        int indexOfEquals = outputLine.IndexOf('=', indexOfTrsEulerAngles);
                        string facingText = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfStatementEnd);
                        string rotatorText = "FQuat." + CONSTANT_INDICATOR + "MakeFromEuler(" + facingText + ").Rotator()";
                        outputLine = outputLine.Replace(statement, "SetActorRotation(" + rotatorText + ", ETeleportType." + CONSTANT_INDICATOR + "TeleportPhysics);");
                    }
                    outputLine = outputLine.Replace("Transform", "FTransform");
                }
                else if (Translator.instance.GetType().Name == "UnityToBevy")
                {
                    int indexOfCurrentKeyboard = 0;
                    while (indexOfCurrentKeyboard != -1)
                    {
                        indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR);
                        if (indexOfCurrentKeyboard != -1)
                        {
                            int indexOfPeriod = outputLine.IndexOf('.', indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                            string key = outputLine.SubstringStartEnd(indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length, indexOfPeriod);
                            string newKey = "";
                            if (key.EndsWith("Key"))
                            {
                                newKey = key.Replace("Key", "");
                                newKey = "Key" + newKey.ToUpper();
                            }
                            int indexOfEndOfClauseAfterKey = outputLine.IndexOfAny(new char[] { '.', ' ', ';', ')' }, indexOfPeriod + 1);
                            string clauseAfterKey = outputLine.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterKey);
                            if (clauseAfterKey == "isPressed")
                                outputLine = outputLine.Replace(CURRENT_KEYBOARD_INDICATOR + key + '.' + clauseAfterKey, "keys.pressed(KeyCode." + CONSTANT_INDICATOR + newKey + ")");
                        }
                    }
                    int indexOfCurrentMouse = 0;
                    while (indexOfCurrentMouse != -1)
                    {
                        indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR);
                        if (indexOfCurrentMouse != -1)
                        {
                            string command = outputLine.Substring(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                            if (command.StartsWith("position.ReadValue()"))
                                outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + "position.ReadValue()", "cursorWorldPoint");
                            // else
                            // {
                            //     int indexOfPeriod = outputLine.IndexOf('.', indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                            //     string button = outputLine.SubstringStartEnd(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length, indexOfPeriod);
                            //     string key = "";
                            //     if (button == "leftButton")
                            //         key = "LeftMouseButton";
                            //     else if (button == "rightButton")
                            //         key = "RightMouseButton";
                            //     int indexOfEndOfClauseAfterButton = outputLine.IndexOfAny(new char[] { '.', ' ', ';', ')' }, indexOfPeriod + 1);
                            //     string clauseAfterButton = outputLine.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterButton);
                            //     if (clauseAfterButton == "isPressed")
                            //         outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + button + '.' + clauseAfterButton, "UGameplayStatics." + CONSTANT_INDICATOR + "GetPlayerController(GetWorld(), 0)." + POINTER_INDICATOR + "IsInputKeyDown(EKeys." + CONSTANT_INDICATOR + key + ")");
                            // }
                        }
                    }
                }
                outputLine = outputLine.Replace(" : MonoBehaviour", ""); // TODO: Make this work with interfaces
                outputLines.Add(outputLine);
            }
            csharpCode = string.Join('\n', outputLines);
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseSyntaxTree(csharpCode).GetRoot();
            return ConvertAndRunCode(engine, csharpAst);
        }

        private static object ConvertAndRunCode(
                EngineWrapper engine,
                Microsoft.CodeAnalysis.SyntaxNode csharpAstNode,
                string[] requiredImports = null) {
            var rewritten = MultiLineLambdaRewriter.RewriteMultiLineLambdas(csharpAstNode);
            var pythonAst = new CSharpToPythonConvert().Visit(rewritten);
            var convertedCode = PythonAstPrinter.PrintPythonAst(pythonAst);
            var extraImports = requiredImports is null ? "" : string.Join("\r\n", requiredImports.Select(i => "import " + i));
            convertedCode = "import clr\r\n" + extraImports + "\r\n" + convertedCode;
            if (pythonAst is PyAst.SuiteStatement suiteStmt) {
                var pythonStatements = suiteStmt.Statements
                    .Where(s => !(s is PyAst.FromImportStatement || s is PyAst.ImportStatement)).ToList();
                // If the AST contained only a function definition, run it
                if (pythonStatements.Count == 1 && pythonStatements.Single() is PyAst.FunctionDefinition funcDef) {
                    convertedCode += $"\r\n{funcDef.Name}()";
                }

                // if (pythonStatements.Count >= 1 && pythonStatements.All(s => s is PyAst.ClassDefinition)) {
                //     var lastClassDef = (PyAst.ClassDefinition)pythonStatements.Last();
                //     convertedCode += $"\r\n{lastClassDef.Name}()";
                // }
            }
            convertedCode = convertedCode.Replace("from", "from_");
            foreach (string member in CSharpToPythonConvert.membersToAdd)
                convertedCode += member + '\n';
            convertedCode = convertedCode.Replace("FFTransform", "FTransform");
            UnityToUnreal.pythonFileContents = convertedCode;
            UnityToBevy.pythonFileContents = convertedCode;
            UnityToGodot.pythonFileContents = convertedCode;
            if (Translator.instance.GetType().Name == "UnityToBevy")
                File.WriteAllText(Environment.CurrentDirectory + "/src/main.py", convertedCode);
            var scope = engine.Engine.CreateScope();
            var source = engine.Engine.CreateScriptSourceFromString(convertedCode, Microsoft.Scripting.SourceCodeKind.AutoDetect);
            try
            {
                return source.Execute(scope);
            }
            catch (Microsoft.Scripting.SyntaxErrorException syntaxErrorException)
            {
                Console.WriteLine("WOW" + syntaxErrorException.TargetSite);
                foreach (var keyValuePair in syntaxErrorException.Data)
                    Console.WriteLine("WOW" + keyValuePair);
                Console.WriteLine(source.GetReader().ReadToEnd());
            }
            catch (Exception e)
            {
                Console.WriteLine("WOW" + e.Message);
            }
            return null;
        }

        public static string ConvertExpressionCode(string csharpCode) {
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(csharpCode);
            return ConvertCsharpAST(csharpAst);
        }

        public static string ConvertCode(string csharpCode) {
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseSyntaxTree(csharpCode).GetRoot();
            return ConvertCsharpAST(csharpAst);
        }

        private static string ConvertCsharpAST(Microsoft.CodeAnalysis.SyntaxNode csharpAst) {
            var rewritten = MultiLineLambdaRewriter.RewriteMultiLineLambdas(csharpAst);
            var pythonAst = new CSharpToPythonConvert().Visit(rewritten);
            return PythonAstPrinter.PrintPythonAst(pythonAst);
        }
    }

    public class EngineWrapper {
        internal readonly Microsoft.Scripting.Hosting.ScriptEngine Engine = IronPython.Hosting.Python.CreateEngine();
    }
}
