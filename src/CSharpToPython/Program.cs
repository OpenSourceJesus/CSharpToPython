using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PyAst = IronPython.Compiler.Ast;

namespace CSharpToPython {
    public class Program
    {
        static string globalVariablesString;
        static string UNREAL_PROJECT_PATH = Environment.CurrentDirectory + "/BareUEProject";
        static string CODE_PATH = UNREAL_PROJECT_PATH + "/Source/BareUEProject";
        static string[] SCREEN_TO_WORLD_POINT_INDICATORS = { "Camera.main.ScreenToWorldPoint(", "GetComponent<Camera>().ScreenToWorldPoint(" };
        const string CURRENT_KEYBOARD_INDICATOR = "Keyboard.current.";
        const string CURRENT_MOUSE_INDICATOR = "Mouse.current.";
        const string CONSTANT_INDICATOR = "const";
        const string POINTER_INDICATOR = "ptr";
        const string INSTANTIATE_INDICATOR = "Instantiate(";
        const string GAME_OBJECT_FIND_INDICATOR = "GameObject.Find(";
        const string CLASS_VARIABLE_INDICATOR = "#💠";
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
                    while (indexOfInstantiate != -1)
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
                        indexOfInstantiate = outputLine.IndexOf(INSTANTIATE_INDICATOR, indexOfInstantiate + INSTANTIATE_INDICATOR.Length);
                    }
                    string positionIndicator = "transform.position";
                    int indexOfPosition = outputLine.IndexOf(positionIndicator);
                    while (indexOfPosition != -1)
                    {
                        int indexOfEquals = outputLine.IndexOf('=', indexOfPosition);
                        if (indexOfEquals != -1)
                        {
                            int indexofSemicolon = outputLine.IndexOf(';', indexOfEquals);
                            string position = outputLine.SubstringStartEnd(indexOfEquals + 1, indexofSemicolon);
                            outputLine = "TeleportTo(" + position + ", GetActorRotation(), true, true);";
                        }
                        indexOfPosition = outputLine.IndexOf(positionIndicator, indexOfPosition + positionIndicator.Length);
                    }
                    int indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR);
                    while (indexOfCurrentKeyboard != -1)
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
                        indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR, indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                    }
                    int indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR);
                    while (indexOfCurrentMouse != -1)
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
                        indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR, indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                    }
                    int indexOfGameObjectFind = outputLine.IndexOf(GAME_OBJECT_FIND_INDICATOR);
                    while (indexOfGameObjectFind != -1)
                    {
                        int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(indexOfGameObjectFind + GAME_OBJECT_FIND_INDICATOR.Length);
                        string gameObjectFind = outputLine.SubstringStartEnd(indexOfGameObjectFind, indexOfRightParenthesis);
                        Console.WriteLine("YAY" + gameObjectFind);
                        string whatToFind = gameObjectFind.SubstringStartEnd(GAME_OBJECT_FIND_INDICATOR.Length, gameObjectFind.Length - 2);
                        outputLine = outputLine.Replace(outputLine, "Utils." + CONSTANT_INDICATOR + "GetActor(" + whatToFind + ", GetWorld())");
                        indexOfGameObjectFind = outputLine.IndexOf(GAME_OBJECT_FIND_INDICATOR, indexOfGameObjectFind + GAME_OBJECT_FIND_INDICATOR.Length);
                    }
                    foreach (string screenToWorldPointIndicator in SCREEN_TO_WORLD_POINT_INDICATORS)
                    {
                        int screenToWorldPointIndicatorIndex = outputLine.IndexOf(screenToWorldPointIndicator);
                        if (screenToWorldPointIndicatorIndex != -1)
                            outputLine = outputLine.Replace(screenToWorldPointIndicator, "Utils." + CONSTANT_INDICATOR + "ScreenToWorldPoint(GetWorld(), ");
                    }
                    string trsUpIndicator = "transform.up";
                    int indexOfTrsUp = outputLine.IndexOf(trsUpIndicator);
                    while (indexOfTrsUp != -1)
                    {
                        int indexOfStatementEnd = outputLine.IndexOf(';', indexOfTrsUp);
                        string statement = outputLine.SubstringStartEnd(indexOfTrsUp, indexOfStatementEnd);
                        int indexOfEquals = outputLine.IndexOf('=', indexOfTrsUp);
                        string facingText = outputLine.SubstringStartEnd(indexOfEquals + 1, indexOfStatementEnd);
                        string rotatorText = "UKismetMathLibrary." + CONSTANT_INDICATOR + "MakeRotFromZ(" + facingText + ")";
                        outputLine = outputLine.Replace(statement, "SetActorRotation(" + rotatorText + ", ETeleportType." + CONSTANT_INDICATOR + "TeleportPhysics);");
                        indexOfTrsUp = outputLine.IndexOf(trsUpIndicator, indexOfTrsUp + trsUpIndicator.Length);
                    }
                    string trsEulerAnglesIndicator = "transform.eulerAngles";
                    int indexOfTrsEulerAngles = outputLine.IndexOf(trsEulerAnglesIndicator);
                    while (indexOfTrsEulerAngles != -1)
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
                        indexOfTrsEulerAngles = outputLine.IndexOf(trsEulerAnglesIndicator, indexOfTrsEulerAngles + trsEulerAnglesIndicator.Length);
                    }
                    outputLine = outputLine.Replace("Transform", "FTransform");
                }
                else if (Translator.instance.GetType().Name == "UnityToBevy")
                {
                    int indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR);
                    while (indexOfCurrentKeyboard != -1)
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
                        indexOfCurrentKeyboard = outputLine.IndexOf(CURRENT_KEYBOARD_INDICATOR, indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                    }
                    int indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR);
                    while (indexOfCurrentMouse != -1)
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
                        indexOfCurrentMouse = outputLine.IndexOf(CURRENT_MOUSE_INDICATOR, indexOfCurrentMouse + 1);
                    }
                    string destroyIndicator = "Destroy(";
                    int indexOfDestroy = outputLine.IndexOf(destroyIndicator);
                    while (indexOfDestroy != -1)
                    {
                        int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(indexOfDestroy + destroyIndicator.Length);
                        string whatToDestroy = outputLine.SubstringStartEnd(indexOfDestroy, indexOfRightParenthesis);
                        if (whatToDestroy == "gameObject" || whatToDestroy == "GetComponent<GameObject>()")
                        {
                            // outputLine = outputLine.Replace(destroyIndicator + whatToDestroy + ')', "commands.entity(sceneEntity).remove<");
                        }
                        indexOfDestroy = outputLine.IndexOf(destroyIndicator, indexOfDestroy + destroyIndicator.Length);
                    }
                    (int index, string whatWasFound) screenToWorldPointIndicatorFindResult = outputLine.IndexOfAny(SCREEN_TO_WORLD_POINT_INDICATORS);
                    while (screenToWorldPointIndicatorFindResult.index != -1)
                    {
                        int indexOfRightParenthesis = outputLine.IndexOfMatchingRightParenthesis(screenToWorldPointIndicatorFindResult.index + screenToWorldPointIndicatorFindResult.whatWasFound.Length);
                        string screenPoint = outputLine.SubstringStartEnd(screenToWorldPointIndicatorFindResult.index + screenToWorldPointIndicatorFindResult.whatWasFound.Length, indexOfRightParenthesis);
                        outputLine = outputLine.Replace(screenToWorldPointIndicatorFindResult.whatWasFound + screenPoint + ')', "GetScreenToWorldPoint(" + screenPoint + ", screenToWorldPointEvent)");
                        screenToWorldPointIndicatorFindResult = outputLine.IndexOfAny(SCREEN_TO_WORLD_POINT_INDICATORS, screenToWorldPointIndicatorFindResult.index + screenToWorldPointIndicatorFindResult.whatWasFound.Length);
                    }
                }
                outputLine = outputLine.Replace(" : MonoBehaviour", ""); // TODO: Make this work with interfaces
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
                input = input.Replace("Vector3.back", "-FVector." + CONSTANT_INDICATOR + "YAxisVector");
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
                string vectorIndicator = "Vector3(";
                int indexOfVectorIndicator = indexOfVectorIndicator = input.IndexOf(vectorIndicator);
                while (indexOfVectorIndicator != -1)
                {
                    int indexOfRightParenthesis = input.IndexOfMatchingRightParenthesis(indexOfVectorIndicator + vectorIndicator.Length);
                    if (indexOfRightParenthesis != -1)
                    {
                        string vectorValue = input.SubstringStartEnd(indexOfVectorIndicator + vectorIndicator.Length, indexOfRightParenthesis);
                        vectorValue = SwapVectorYAndZ(vectorValue);
                        input = input.Remove(indexOfVectorIndicator, vectorIndicator.Length + vectorValue.Length);
                        input = input.Insert(indexOfVectorIndicator, "FVector(" + vectorValue);
                    }
                    indexOfVectorIndicator = input.IndexOf(vectorIndicator, indexOfVectorIndicator + vectorIndicator.Length);
                }
            }
            else if (Translator.instance.GetType().Name == "UnityInBlender")
            {
                input = input.Replace("Time.deltaTime", "0.016666667");
                input = input.Replace("Vector3.zero", "mathutils.Vector()");
                input = input.Replace("Vector3.right", "mathutils.Vector((1, 0, 0))");
                input = input.Replace("Vector3.left", "mathutils.Vector((-1, 0, 0))");
                input = input.Replace("Vector3.up", "mathutils.Vector((0, 0, 1))");
                input = input.Replace("Vector3.down", "mathutils.Vector((0, 0, -1))");
                input = input.Replace("Vector3.forward", "mathutils.Vector((0, 1, 0))");
                input = input.Replace("Vector3.back", "mathutils.Vector((0, -1, 0))");
                input = input.Replace("Vector3.one", "mathutils.Vector((1, 1, 1))");
                input = input.Replace("transform.eulerAngles", "self.rotation_euler");
                input = input.Replace("transform.position", "self.location");
                int spaceCountBeforeInput = input.Length - input.TrimStart().Length;
                string spaceBeforeInput = input.Substring(0, spaceCountBeforeInput);
                string[] lines = input.Split('\n');
                for (int i = 0; i < lines.Length; i ++)
                {
                    string line = lines[i];
                    string vectorIndicator = "Vector3(";
                    int indexOfVectorIndicator = indexOfVectorIndicator = line.IndexOf(vectorIndicator);
                    while (indexOfVectorIndicator != -1)
                    {
                        int indexOfRightParenthesis = line.IndexOfMatchingRightParenthesis(indexOfVectorIndicator + vectorIndicator.Length);
                        if (indexOfRightParenthesis != -1)
                        {
                            string vectorValue = line.SubstringStartEnd(indexOfVectorIndicator + vectorIndicator.Length, indexOfRightParenthesis);
                            vectorValue = SwapVectorYAndZ(vectorValue);
                            line = line.Remove(indexOfVectorIndicator, vectorIndicator.Length + vectorValue.Length);
                            line = line.Insert(indexOfVectorIndicator, "mathutils.Vector((" + vectorValue + ')');
                        }
                        indexOfVectorIndicator = line.IndexOf(vectorIndicator, indexOfVectorIndicator + vectorIndicator.Length);
                    }
                    string trsEulerAnglesIndicator = "self.rotation_euler";
                    int indexOfTrsEulerAngles = line.IndexOf(trsEulerAnglesIndicator);
                    while (indexOfTrsEulerAngles != -1)
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
                                line = line.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "rotation_ = mathutils.Euler(" + valueAfterEquals + " / 57.2958)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('X', rotation_.x)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('Y', rotation_.y)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('Z', rotation_.z)");
                            }
                            else if (textBetweenTrsEulerAnglesAndEquals.Trim() == "-")
                            {
                                string valueAfterEquals = line.Substring(indexOfEquals + 1);
                                line = line.Replace(trsEulerAnglesIndicator + textBetweenTrsEulerAnglesAndEquals + '=' + valueAfterEquals, "rotation_ = mathutils.Euler(" + valueAfterEquals + " / -57.2958)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('X', rotation_.x)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('Y', rotation_.y)\n" + spaceBeforeInput + "self.rotation_euler.rotate_axis('Z', rotation_.z)");
                            }
                        }
                        indexOfTrsEulerAngles = line.IndexOf(trsEulerAnglesIndicator, indexOfTrsEulerAngles + trsEulerAnglesIndicator.Length);
                    }
                    string trsPositionIndicator = "self.location";
                    int indexOfTrsPosition = line.IndexOf(trsPositionIndicator);
                    while (indexOfTrsPosition != -1)
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
                        indexOfTrsPosition = line.IndexOf(trsPositionIndicator, indexOfTrsPosition + trsPositionIndicator.Length);
                    }
                    int indexOfCurrentKeyboard = line.IndexOf(CURRENT_KEYBOARD_INDICATOR);
                    while (indexOfCurrentKeyboard != -1)
                    {
                        int indexOfPeriod = line.IndexOf('.', indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                        string key = line.SubstringStartEnd(indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length, indexOfPeriod);
                        string newKey = key.Replace("Key", "");
                        int indexOfEndOfClauseAfterKey = line.IndexOfAny(new char[] { '.', ' ', ';', ')', ':' }, indexOfPeriod + 1);
                        string clauseAfterKey = line.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterKey);
                        if (clauseAfterKey == "isPressed")
                            line = line.Replace(CURRENT_KEYBOARD_INDICATOR + key + '.' + clauseAfterKey, "\'" + newKey + "\' in keysPressed_");
                        indexOfCurrentKeyboard = line.IndexOf(CURRENT_KEYBOARD_INDICATOR, indexOfCurrentKeyboard + CURRENT_KEYBOARD_INDICATOR.Length);
                    }
                    int indexOfCurrentMouse = line.IndexOf(CURRENT_MOUSE_INDICATOR);
                    while (indexOfCurrentMouse != -1)
                    {
                        string command = line.Substring(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                        if (command.StartsWith("position.ReadValue()"))
                            line = line.Replace(CURRENT_MOUSE_INDICATOR + "position.ReadValue()", "mousePosition_");
                        else
                        {
                            int indexOfPeriod = line.IndexOf('.', indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                            string button = line.SubstringStartEnd(indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length, indexOfPeriod);
                            string newButton = button.Replace("Button", "");
                            int indexOfEndOfClauseAfterButton = line.IndexOfAny(new char[] { '.', ' ', ';', ')', ':' }, indexOfPeriod + 1);
                            string clauseAfterButton = line.SubstringStartEnd(indexOfPeriod + 1, indexOfEndOfClauseAfterButton);
                            if (clauseAfterButton == "isPressed")
                                line = line.Replace(CURRENT_MOUSE_INDICATOR + button + '.' + clauseAfterButton, '\'' + newButton + "\' in mouseButtonsPressed_");
                        }
                        indexOfCurrentMouse = line.IndexOf(CURRENT_MOUSE_INDICATOR, indexOfCurrentMouse + CURRENT_MOUSE_INDICATOR.Length);
                    }
                    string normalizeIndicator = ".Normalize";
                    int indexOfNormalizeIndicator = line.IndexOf(normalizeIndicator);
                    while (indexOfNormalizeIndicator != -1)
                    {
                        int indexOfWhatToNormalize = line.LastIndexOfAny(new char[] { '.', ' ', ';', ')', ':' }, indexOfNormalizeIndicator - 1);
                        if (indexOfWhatToNormalize != -1)
                            indexOfWhatToNormalize ++;
                        string whatToNormalize = line.SubstringStartEnd(indexOfWhatToNormalize, indexOfNormalizeIndicator);
                        if (whatToNormalize.IsAlphaNumeric())
                        {
                            string type = GetVariableType(string.Join('\n', lines), whatToNormalize);
                            if (type == "Vector2" || type == "Vector3" || type == "Vector4")
                            {
                                line = line.Remove(indexOfWhatToNormalize, normalizeIndicator.Length);
                                line = line.Insert(indexOfWhatToNormalize, ".normalize");
                            }
                        }
                        indexOfNormalizeIndicator = line.IndexOf(normalizeIndicator, indexOfNormalizeIndicator + normalizeIndicator.Length);
                    }
                    foreach (string screenToWorldPointIndicator in SCREEN_TO_WORLD_POINT_INDICATORS)
                        line = line.Replace(screenToWorldPointIndicator, "ScreenToWorldPoint(");
                    string newGameObjectIndicator = "GameObject()";
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
                string membersString = "";
                foreach (string member in CSharpToPythonConvert.membersToAdd)
                {
                    membersString += member + '\n';
                    Console.WriteLine("WOWOW" + member);
                }
                if (Translator.instance.GetType().Name == "UnityInBlender")
                {
                    globalVariablesString = "global mousePosition_, mouseButtonsPressed_, keysPressed_, ";
                    int indexOfClassVariableIndicator = -CLASS_VARIABLE_INDICATOR.Length;
                    while (indexOfClassVariableIndicator != -1)
                    {
                        indexOfClassVariableIndicator = convertedCode.IndexOf(CLASS_VARIABLE_INDICATOR, indexOfClassVariableIndicator + CLASS_VARIABLE_INDICATOR.Length);
                        if (indexOfClassVariableIndicator != -1)
                        {
                            int indexOfColon = convertedCode.IndexOf(':', indexOfClassVariableIndicator);
                            globalVariablesString += convertedCode.SubstringStartEnd(indexOfClassVariableIndicator + CLASS_VARIABLE_INDICATOR.Length, indexOfColon) + ", ";
                        }
                    }
                    globalVariablesString = globalVariablesString.Substring(0, globalVariablesString.Length - 2);
                    string[] lines = convertedCode.Split("\n");
                    Console.WriteLine("Original:" + convertedCode);
                    for (int i = 0; i < lines.Length; i ++)
                    {
                        string line = lines[i];
                        int indexOfInitMethod = line.IndexOf("def __init__(self)");
                        if (indexOfInitMethod != -1)
                        {
                            string method = line;
                            i ++;
                            while (i < lines.Length)
                            {
                                line = lines[i];
                                int spaceCountBeforeInitMethod = indexOfInitMethod;
                                if (i == lines.Length - 1 || line.Length < spaceCountBeforeInitMethod || !string.IsNullOrWhiteSpace(line.Substring(0, spaceCountBeforeInitMethod + 1)))
                                {
                                    convertedCode = convertedCode.Replace(method, "");
                                    break;
                                }
                                else
                                    method += '\n' + line;
                                i ++;
                            }
                        }
                        int indexOfUpdateMethod = line.IndexOf("def Update");
                        if (indexOfUpdateMethod != -1)
                        {
                            int spaceCountBeforeUpdateMethod = indexOfUpdateMethod;
                            string oldMethod = line;
                            string newMethod = line.Substring(spaceCountBeforeUpdateMethod);
                            i ++;
                            int spaceCountAtUpdateMethodStart = line.Length - line.TrimStart().Length;
                            string spaceAtUpdateMethodStart = line.Substring(0, spaceCountAtUpdateMethodStart);
                            line = lines[i];
                            newMethod += '\n' + spaceAtUpdateMethodStart + globalVariablesString;
                            while (i < lines.Length)
                            {
                                line = lines[i];
                                if (i == lines.Length - 1 || line.Length < spaceCountBeforeUpdateMethod || !string.IsNullOrWhiteSpace(line.Substring(0, spaceCountBeforeUpdateMethod + 1)))
                                {
                                    convertedCode = convertedCode.Replace(oldMethod, newMethod);
                                    break;
                                }
                                else
                                {
                                    oldMethod += '\n' + line;
                                    newMethod += '\n' + Translate(line.Substring(spaceCountBeforeUpdateMethod).Replace("self.", ""));
                                }
                                i ++;
                            }
                        }
                        else
                        {
                            int indexOfClass = line.IndexOf("class ");
                            if (indexOfClass != -1)
                                convertedCode = convertedCode.Replace(line, "");
                        }
                    }
                    convertedCode = membersString + convertedCode + "Update (self)";
                }
                else
                    convertedCode += membersString;
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

        static string SwapVectorYAndZ (string vectorValue)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(vectorValue);
            IEnumerable<SyntaxNode> nodes = tree.GetRoot().ChildNodes();
            return nodes.Get(0) + ", " + nodes.Get(2) + ", " + nodes.Get(1);
        }

        static string GetVariableType (string csCode, string variable)
        {
            int indexOfVariable = csCode.IndexOf(variable);
            while (indexOfVariable != -1)
            {
                int indexOfType = csCode.LastIndexOfAny(new char[] { ',', ';', ' ', '\t', '\n' }, indexOfVariable - 1);
                if (indexOfType != -1)
                    indexOfType ++;
                else
                    break;
                string type = csCode.SubstringStartEnd(indexOfType, indexOfVariable);
                if (type.IsAlphaNumeric())
                    return type;
                indexOfVariable = csCode.IndexOf(variable, indexOfVariable + variable.Length);
            }
            throw new Exception("Couldn't get the type for '" + variable + "' in the code '" + csCode + "'");
        }
    }

    public class EngineWrapper {
        internal readonly Microsoft.Scripting.Hosting.ScriptEngine Engine = IronPython.Hosting.Python.CreateEngine();
    }
}
