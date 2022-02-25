using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using BepInEx.Logging;

namespace SpawnFlightPatch
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        private static ManualLogSource logger = Logger.CreateLogSource("SpawnFlightPatch");

        public static void Patch(AssemblyDefinition assembly)
        {  
            TypeDefinition StrategicTarget2 = assembly.MainModule.Types.Where(type => type.IsClass && type.Namespace == "Falcon.Game2.Arena2" && type.Name == "StrategicTarget2").First();
            if (StrategicTarget2 != null)
            {
                logger.LogDebug("Found StrategicTarget2");
                MethodDefinition SpawnFlight = StrategicTarget2.Methods.Where(method => method.Name == "SpawnFlight").First();

                if (SpawnFlight != null)
                {
                    logger.LogDebug("Found SpawnFlight");
                    TypeReference UniFlight = GetUniFlightType(assembly);
                    if (SpawnFlight.ReturnType == UniFlight)
                    {
                        logger.LogInfo("Return Type Already Patched");
                        return;
                    }
                    SpawnFlight.ReturnType = UniFlight;

                    Instruction ret = SpawnFlight.Body.Instructions.Last();

                    // Remove pop before ret in SpawnFlight;
                    SpawnFlight.Body.GetILProcessor().Remove(ret.Previous);
                }


                MethodDefinition InitializeForArena = StrategicTarget2.Methods.Where(method => method.Name == "InitializeForArena").First();
                if (InitializeForArena != null)
                {
                    logger.LogDebug("Found InitializeForArena");
                    Instruction callSpawnFlight = InitializeForArena.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Call && instruction.Operand == SpawnFlight.GetElementMethod()).First();
                    InitializeForArena.Body.GetILProcessor().InsertAfter(callSpawnFlight, InitializeForArena.Body.GetILProcessor().Create(OpCodes.Pop));
                }
            }
            else
            {
                logger.LogError("Unable to find StrategicTarget2");
            }            
            logger.LogInfo("Patch complete");
        }

        private static TypeReference GetUniFlightType(AssemblyDefinition assembly)
        {            
            TypeDefinition UniFlight = assembly.MainModule.Types.Where(type => type.IsClass && type.Namespace == "Falcon.UniversalAircraft" && type.Name == "UniFlight").First();
            if (UniFlight != null)
            {
                logger.LogDebug("Found UniFlight");

                return UniFlight.GetElementType();
            }
            else
            {
                logger.LogError("Unable to find UniFlight");
            }
            
            return null;
        }
    }
}
