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
        const string GAME_OBJECT_FIND_INDICATOR = "GameObject.Find(";
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
                    outputLine = Translate(outputLine);
                    int indexOfInstantiate = outputLine.IndexOf(INSTANTIATE_INDICATOR);
                    if (indexOfInstantiate != -1)
                    {
                        // string replaceWith = "Utils." + CONSTANT_INDICATOR + "SpawnActor(GetWorld(), ";
                        string replaceWith = "SpawnActor(";
                        int indexOfComma = outputLine.IndexOf(',', indexOfInstantiate);
                        string argument1 = outputLine.SubstringStartEnd(indexOfInstantiate + INSTANTIATE_INDICATOR.Length, indexOfComma);
                        replaceWith += argument1;
                        int indexOfComma2 = outputLine.IndexOf(',', indexOfComma + 1);
                        string argument2 = outputLine.SubstringStartEnd(indexOfComma + 1, indexOfComma2);
                        replaceWith += ", " + argument2;
                        int indexOfParenthesis = outputLine.IndexOfMatchingRightParenthesis(indexOfInstantiate + INSTANTIATE_INDICATOR.Length - 1);
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
                    int indexOfGameObjectFind = 0;
                    while (indexOfGameObjectFind != -1)
                    {
                        indexOfGameObjectFind = outputLine.IndexOf(GAME_OBJECT_FIND_INDICATOR);
                        if (indexOfGameObjectFind != -1)
                        {
                            int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(indexOfGameObjectFind + GAME_OBJECT_FIND_INDICATOR.Length);
                            string gameObjectFind = outputLine.SubstringStartEnd(indexOfGameObjectFind, indexOfRightParenthesis);
                            Console.WriteLine("YAY" + gameObjectFind);
                            string whatToFind = gameObjectFind.SubstringStartEnd(GAME_OBJECT_FIND_INDICATOR.Length, gameObjectFind.Length - 2);
                            outputLine = outputLine.Replace(outputLine, "Utils." + CONSTANT_INDICATOR + "GetActor(" + whatToFind + ", GetWorld())");
                        }
                    }
                    foreach (string screenToWorldPointIndicator in SCREEN_TO_WORLD_POINT_INDICATORS)
                    {
                        int screenToWorldPointIndicatorIndex = outputLine.IndexOf(screenToWorldPointIndicator);
                        if (screenToWorldPointIndicatorIndex != -1)
                            outputLine = outputLine.Replace(screenToWorldPointIndicator, "Utils." + CONSTANT_INDICATOR + "ScreenToWorldPoint(GetWorld(), ");
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
                    string trsEulerAnglesIndicator = "transform.eulerAngles";
                    int indexOfTrsEulerAngles = outputLine.IndexOf(trsEulerAnglesIndicator);
                    if (indexOfTrsEulerAngles != -1)
                    {
                        int indexOfStatementEnd = outputLine.IndexOf(';', indexOfTrsEulerAngles);
                        string statement = outputLine.SubstringStartEnd(indexOfTrsEulerAngles, indexOfStatementEnd);
                        int indexOfEquals = outputLine.IndexOf('=', indexOfTrsEulerAngles);
                        if (indexOfEquals != -1)
                        {
                            string textBetweenTrsEulerAnglesAndEquals = outputLine.SubstringStartEnd(indexOfTrsEulerAngles + trsEulerAnglesIndicator.Length, indexOfEquals);
                            if (textBetweenTrsEulerAnglesAndEquals == "" || string.IsNullOrWhiteSpace(textBetweenTrsEulerAnglesAndEquals))
                            {
                                string facingText = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfStatementEnd);
                                string rotatorText = "FQuat." + CONSTANT_INDICATOR + "MakeFromEuler(" + facingText + ").Rotator()";
                                outputLine = outputLine.Replace(statement, "SetActorRotation(" + rotatorText + ", ETeleportType." + CONSTANT_INDICATOR + "TeleportPhysics);");
                            }
                            else if (textBetweenTrsEulerAnglesAndEquals.Trim() == "+")
                            {
                                int indexOfSemicolon = outputLine.IndexOf(';', indexOfEquals);
                                string valueAfterEquals = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfSemicolon);
                                outputLine = outputLine.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "AddActorWorldRotation(FRotator." + CONSTANT_INDICATOR + "MakeFromEuler(" + Translate(valueAfterEquals) + "))");
                            }
                            else if (textBetweenTrsEulerAnglesAndEquals.Trim() == "-")
                            {
                                int indexOfSemicolon = outputLine.IndexOf(';', indexOfEquals);
                                string valueAfterEquals = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfSemicolon);
                                outputLine = outputLine.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "AddActorWorldRotation(FRotator." + CONSTANT_INDICATOR + "MakeFromEuler(-" + Translate(valueAfterEquals) + "))");
                            }
                            else
                            {
                                outputLine = outputLine.Remove(indexOfTrsEulerAngles, trsEulerAnglesIndicator.Length);
                                outputLine = outputLine.Insert(indexOfTrsEulerAngles, "GetActorRotation().Euler()");
                            }
                        }
                        else
                        {
                            outputLine = outputLine.Remove(indexOfTrsEulerAngles, trsEulerAnglesIndicator.Length);
                            outputLine = outputLine.Insert(indexOfTrsEulerAngles, "GetActorRotation().Euler()");
                        }
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
                                outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + "position.ReadValue()", "cursorPoint");
                            else
                            {
                                int indexOfPeriod = outputLine.IndexOf('.', indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                                string button = outputLine.SubstringStartEnd(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length, indexOfPeriod);
                                string newButton = "";
                                if (button == "leftButton")
                                    newButton = "Left";
                                else if (button == "rightButton")
                                    newButton = "Right";
                                int indexOfEndOfClauseAfterButton = outputLine.IndexOfAny(new char[] { '.', ' ', ';', ')' }, indexOfPeriod + 1);
                                string clauseAfterButton = outputLine.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterButton);
                                if (clauseAfterButton == "isPressed")
                                    outputLine = outputLine.Replace(CURRENT_MOUSE_INDICATOR + button + '.' + clauseAfterButton, "mouseButtons.pressed(MouseButton." + CONSTANT_INDICATOR + newButton + ")");
                            }
                        }
                    }
                    int indexOfDestroy = 0;
                    while (indexOfDestroy != -1)
                    {
                        string destroyIndicator = "Destroy(";
                        indexOfDestroy = outputLine.IndexOf(destroyIndicator);
                        if (indexOfDestroy != -1)
                        {
                            int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(indexOfDestroy + destroyIndicator.Length);
                            string whatToDestroy = outputLine.SubstringStartEnd(indexOfDestroy, indexOfRightParenthesis);
                            if (whatToDestroy == "gameObject" || whatToDestroy == "GetComponent<GameObject>()")
                            {
                                // outputLine = outputLine.Replace(destroyIndicator + whatToDestroy + ')', "commands.entity(sceneEntity).remove<");
                            }
                        }
                    }
                    foreach (string screenToWorldPointIndicator in SCREEN_TO_WORLD_POINT_INDICATORS)
                    {
                        int screenToWorldPointIndicatorIndex = outputLine.IndexOf(screenToWorldPointIndicator);
                        if (screenToWorldPointIndicatorIndex != -1)
                        {
                            int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(screenToWorldPointIndicatorIndex + screenToWorldPointIndicator.Length);
                            string screenPoint = outputLine.SubstringStartEnd(screenToWorldPointIndicatorIndex + screenToWorldPointIndicator.Length, indexOfRightParenthesis);
                            outputLine = outputLine.Replace(screenToWorldPointIndicator + screenPoint + ')', "GetScreenToWorldPoint(" + screenPoint + ", screenToWorldPointEvent)");
                        }
                    }
                }
                // outputLine = outputLine.Replace(" : MonoBehaviour", ""); // TODO: Make this work with interfaces
                outputLines.Add(outputLine);
            }
            csharpCode = string.Join('\n', outputLines);
            var csharpAst = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseSyntaxTree(csharpCode).GetRoot();
            return ConvertAndRunCode(engine, csharpAst);
        }

        static string Translate (string input)
        {
            if (Translator.instance.GetType().Name == "UnityToUnreal")
            {
                input = input.Replace("Time.time", "UGameplayStatics." + CONSTANT_INDICATOR + "GetRealTimeSeconds(GetWorld())");
                input = input.Replace("Time.deltaTime", "UGameplayStatics." + CONSTANT_INDICATOR + "GetWorldDeltaSeconds(GetWorld())");
                input = input.Replace("Mathf.Sin", "FMath." + CONSTANT_INDICATOR + "Sin");
                input = input.Replace("Mathf.Cos", "FMath." + CONSTANT_INDICATOR + "Cos");
                input = input.Replace("Vector3.right", "-FVector." + CONSTANT_INDICATOR + "XAxisVector");
                input = input.Replace("Vector3.left", "FVector." + CONSTANT_INDICATOR + "XAxisVector");
                input = input.Replace("Vector3.forward", "FVector." + CONSTANT_INDICATOR + "YAxisVector");
                input = input.Replace("Vector3.up", "FVector." + CONSTANT_INDICATOR + "ZAxisVector");
                input = input.Replace("Vector3.down", "-FVector." + CONSTANT_INDICATOR + "ZAxisVector");
                input = input.Replace("Mathf.Atan2", "UKismetMathLibrary." + CONSTANT_INDICATOR + "Atan2");
                input = input.Replace("transform.position", "GetActorLocation()");
                input = input.Replace("transform.rotation", "GetActorRotation()");
                input = input.Replace("transform.up", "GetActorRightVector()");
                input = input.Replace("Vector3.zero", "FVector." + CONSTANT_INDICATOR + "ZeroVector");
                input = input.Replace(".x", ".X");
                input = input.Replace(".y", ".Z");
                input = input.Replace(".z", ".Y");
                input = input.Replace("Vector2", "FVector2D");
                input = input.Replace("Vector3", "FVector");
            }
            else if (Translator.instance.GetType().Name == "UnityInBlender")
            {
                input = input.Replace("Time.deltaTime", "0.016666667");
                input = input.Replace("Vector3.right", "mathutils.Vector((1, 0, 0))");
                input = input.Replace("Vector3.up", "mathutils.Vector((0, 0, 1))");
                input = input.Replace("Vector3.forward", "mathutils.Vector((0, 1, 0))");
                input = input.Replace("Vector3.left", "mathutils.Vector((-1, 0, 0))");
                input = input.Replace("Vector3.down", "mathutils.Vector((0, 0, -1))");
                input = input.Replace("Vector3.back", "mathutils.Vector((0, -1, 0))");
                input = input.Replace("transform.eulerAngles", "self.rotation_euler");
                input = input.Replace("transform.position", "self.location");
                string[] lines = input.Split('\n');
                for (int i = 0; i < lines.Length; i ++)
                {
                    string line = lines[i];
                    string vectorIndicator = "new Vector3(";
                    int indexOfVectorIndicator = 0;
                    while (indexOfVectorIndicator != -1)
                    {
                        indexOfVectorIndicator = line.IndexOf(vectorIndicator, indexOfVectorIndicator + 1);
                        if (indexOfVectorIndicator != -1)
                        {
                            int indexOfRightParenthesis = line.IndexOfMatchingRightParenthesis(indexOfVectorIndicator + vectorIndicator.Length);
                            if (indexOfRightParenthesis != -1)
                            {
                                Console.WriteLine("YAY");
                                string vectorValue = line.SubstringStartEnd(indexOfVectorIndicator + vectorIndicator.Length, indexOfRightParenthesis);
                                line = line.Remove(indexOfVectorIndicator, vectorIndicator.Length);
                                line = line.Insert(indexOfVectorIndicator, "mathutils.Vector(" + vectorValue + ')');
                            }
                        }
                    }
                    string trsEulerAnglesIndicator = "self.rotation_euler";
                    int indexOfTrsEulerAngles = 0;
                    while (indexOfTrsEulerAngles != -1)
                    {
                        indexOfTrsEulerAngles = line.IndexOf(trsEulerAnglesIndicator, indexOfTrsEulerAngles + 1);
                        if (indexOfTrsEulerAngles != -1)
                        {
                            string statement = line.Substring(indexOfTrsEulerAngles);
                            int indexOfEquals = line.IndexOf('=', indexOfTrsEulerAngles);
                            if (indexOfEquals != -1)
                            {
                                string textBetweenTrsEulerAnglesAndEquals = line.SubstringStartEnd(indexOfTrsEulerAngles + trsEulerAnglesIndicator.Length, indexOfEquals);
                                if (textBetweenTrsEulerAnglesAndEquals == "" || string.IsNullOrWhiteSpace(textBetweenTrsEulerAnglesAndEquals))
                                {
                                    string facingText = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(statement, "self.rotation_euler = mathutils.Euler(" + facingText + ')');
                                }
                                else if (textBetweenTrsEulerAnglesAndEquals.Trim() == "+")
                                {
                                    string valueAfterEquals = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "rotation_ = mathutils.Euler(" + valueAfterEquals + " / 57.2958)\nself.rotation_euler.rotate_axis('X', rotation_.x)\nself.rotation_euler.rotate_axis('Y', rotation_.y)\nself.rotation_euler.rotate_axis('Z', rotation_.z)");
                                }
                                else if (textBetweenTrsEulerAnglesAndEquals.Trim() == "-")
                                {
                                    string valueAfterEquals = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "rotation_ = mathutils.Euler(" + valueAfterEquals + " / -57.2958)\nself.rotation_euler.rotate_axis('X', rotation_.x)\nself.rotation_euler.rotate_axis('Y', rotation_.y)\nself.rotation_euler.rotate_axis('Z', rotation_.z)");
                                }
                            }
                        }
                    }
                    string trsPositionIndicator = "self.location";
                    int indexOfTrsPosition = 0;
                    while (indexOfTrsPosition != -1)
                    {
                        indexOfTrsPosition = line.IndexOf(trsPositionIndicator, indexOfTrsPosition + 1);
                        if (indexOfTrsPosition != -1)
                        {
                            string statement = line.Substring(indexOfTrsPosition);
                            int indexOfEquals = line.IndexOf('=', indexOfTrsPosition);
                            if (indexOfEquals != -1)
                            {
                                string textBetweenTrsPositionAndEquals = line.SubstringStartEnd(indexOfTrsPosition + trsPositionIndicator.Length, indexOfEquals);
                                if (textBetweenTrsPositionAndEquals == "" || string.IsNullOrWhiteSpace(textBetweenTrsPositionAndEquals))
                                {
                                    string positionText = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(statement, "self.location = mathutils.Vector(" + positionText + ')');
                                }
                                else if (textBetweenTrsPositionAndEquals.Trim() == "+")
                                {
                                    string valueAfterEquals = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(trsPositionIndicator + textBetweenTrsPositionAndEquals + '=' + valueAfterEquals, "self.location += mathutils.Vector(" +  valueAfterEquals + ')');
                                }
                                else if (textBetweenTrsPositionAndEquals.Trim() == "-")
                                {
                                    string valueAfterEquals = line.Substring(indexOfEquals + 1);
                                    line = line.Replace(trsPositionIndicator + textBetweenTrsPositionAndEquals + '=' + valueAfterEquals, "self.location -= mathutils.Vector(" +  valueAfterEquals + ')');
                                }
                            }
                        }
                    }
                    string newGameObjectIndicator = "new GameObject()";
                    int indexOfNewGameObjectIndicator = line.IndexOf(newGameObjectIndicator);
                    if (indexOfNewGameObjectIndicator != -1)
                    {
                        string textBeforeNewGameObjectIndicator = line.Substring(0, indexOfNewGameObjectIndicator);
                        if (!string.IsNullOrEmpty(textBeforeNewGameObjectIndicator))
                        {

                        }
                    }
                    lines[i] = line;
                }
                input = string.Join('\n', lines);
            }
            return input;
        }

        private static object ConvertAndRunCode(
                EngineWrapper engine,
                Microsoft.CodeAnalysis.SyntaxNode csharpAstNode,
                string[] requiredImports = null) {
            var rewritten = MultiLineLambdaRewriter.RewriteMultiLineLambdas(csharpAstNode);
            var pythonAst = new CSharpToPythonConvert().Visit(rewritten);
            var convertedCode = PythonAstPrinter.PrintPythonAst(pythonAst);
            var extraImports = requiredImports is null ? "" : string.Join("\r\n", requiredImports.Select(i => "import " + i));
            convertedCode = extraImports + "\r\n" + convertedCode;
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
            if (Translator.instance.GetType().Name != "CSToPython")
            {
                convertedCode = convertedCode.Replace("from", "from_");
                if (Translator.instance.GetType().Name == "UnityInBlender")
                {
                    string membersString = "";
                    foreach (string member in CSharpToPythonConvert.membersToAdd)
                    {
                        membersString += member + '\n';
                        Console.WriteLine("WOWOW" + member);
                    }
                    string[] lines = convertedCode.Split("\n");
                    for (int i = 0; i < lines.Length; i ++)
                    {
                        string line = lines[i];
                        int indexOfUpdateMethod = line.IndexOf("def Update(self)");
                        if (indexOfUpdateMethod != -1)
                        {
                            string updateMethod = "";
                            for (int i2 = i + 1; i2 < lines.Length; i2 ++)
                            {
                                string line2 = lines[i2];
                                if (i2 == lines.Length - 1 || line2.Length <= indexOfUpdateMethod || !Char.IsWhiteSpace(line2[indexOfUpdateMethod + 1]))
                                {
                                    convertedCode = membersString + Translate(updateMethod.Trim());
                                    break;
                                }
                                else
                                    updateMethod += '\n' + line2;
                            }
                            break;
                        }
                    }
                }
                else
                {
                    foreach (string member in CSharpToPythonConvert.membersToAdd)
                    {
                        convertedCode += member + '\n';
                        Console.WriteLine("WOWOW" + member);
                    }
                }
                convertedCode = convertedCode.Replace("FFTransform", "FTransform");
                Translator.pythonFileContents = convertedCode;
                if (Translator.instance.GetType().Name == "UnityToBevy")
                {
                    string[] dataLines = File.ReadAllLines("/tmp/Unity2Many Data (UnityToBevy)");
                    string outputPath = "";
                    foreach (string data in dataLines)
                    {
                        string outputIndicator = "output=";
                        if (data.StartsWith(outputIndicator))
                        {
                            outputPath = data.Substring(outputIndicator.Length);
                            outputPath = outputPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                            break;
                        }
                    }
                    File.WriteAllText(outputPath + "/src/main.py", convertedCode);
                }
            }
            else
                Translator.pythonFileContents = convertedCode;
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
