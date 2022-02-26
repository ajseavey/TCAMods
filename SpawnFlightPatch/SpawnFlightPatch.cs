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
            ModuleDefinition MainModule = assembly.MainModule;
            Dictionary<string, TypeDefinition> Arena2 = MainModule.Types.Where(type => type.IsClass && type.Namespace == "Falcon.Game2.Arena2").ToDictionary(type => type.Name);
            PatchStrategicTarget2(Arena2["StrategicTarget2"], GetUniFlightType(assembly));
            PatchArenaAirfield(Arena2["ArenaAirfield"], MainModule);
        }
        
        private static void PatchArenaAirfield(TypeDefinition ArenaAirfield, ModuleDefinition Falcon)
        {
            TypeReference floatType = Falcon.TypeSystem.Single;
            FieldDefinition RespawnTime = new FieldDefinition("RespawnTime", FieldAttributes.Public, floatType);
            ArenaAirfield.Fields.Add(RespawnTime);
        }

        private static void PatchStrategicTarget2(TypeDefinition StrategicTarget2, TypeReference UniFlight)
        {
            if (StrategicTarget2 != null)
            {
                logger.LogDebug("Found StrategicTarget2");
                MethodDefinition SpawnFlight = StrategicTarget2.Methods.Where(method => method.Name == "SpawnFlight").First();

                if (SpawnFlight != null)
                {
                    logger.LogDebug("Found SpawnFlight");
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
