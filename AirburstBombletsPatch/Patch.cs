using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using BepInEx.Logging;

namespace AirburstBombletsPatch
{
    public class AirburstBombletsPatch
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        private static ManualLogSource logger = Logger.CreateLogSource("AirburstBombletsPatch");

        public static void Patch(AssemblyDefinition assembly)
        {
            ModuleDefinition MainModule = assembly.MainModule;
            Dictionary<string, TypeDefinition> Stores = MainModule.Types.Where(type => type.Namespace == "Falcon.Stores").ToDictionary(type => type.Name);

            TypeDefinition Bomblet = Stores["Bomblet"];
            AddOverlapHits(MainModule, Bomblet);
            PatchFixedUpdate(MainModule, Bomblet);

            TypeDefinition BombletData = CreateBombletData(MainModule, Stores["FuzeType"]);
            TypeDefinition WarheadProperties = Stores["WarheadProperties"];
            AddBombletToWarheadProperties(MainModule, WarheadProperties, BombletData);
        }

        private static void AddOverlapHits(ModuleDefinition MainModule, TypeDefinition Bomblet)
        {
            System.Reflection.Assembly Patch = typeof(AirburstBombletsPatch).Assembly;
            string Root = Patch.Location.Replace($"\\BepInEx\\patchers\\AirburstBombletsPatch.dll", "");
            string Managed = $"{Root}\\Arena_Data\\Managed";
            AssemblyDefinition Physics = AssemblyDefinition.ReadAssembly($"{Managed}\\UnityEngine.PhysicsModule.dll");
            TypeReference Collider = MainModule.ImportReference(Physics.MainModule.Types.Where(type => type.Namespace == "UnityEngine" && type.Name == "Collider").First().GetElementType());
            FieldDefinition overlapHits = new FieldDefinition("overlapHits", FieldAttributes.Private | FieldAttributes.Static, new ArrayType(Collider));
            Bomblet.Fields.Add(overlapHits);

            MethodDefinition cctor = Bomblet.Methods.Where(method => method.IsConstructor && method.IsStatic && method.Name == ".cctor").First();
            ILProcessor processor = cctor.Body.GetILProcessor();
            // Bomblet.overlapHits = new Collider[8];
            processor.InsertBefore(cctor.Body.Instructions.Last(), processor.Create(OpCodes.Ldc_I4_8));
            processor.InsertBefore(cctor.Body.Instructions.Last(), processor.Create(OpCodes.Newarr, Collider));
            processor.InsertBefore(cctor.Body.Instructions.Last(), processor.Create(OpCodes.Stsfld, overlapHits));
        }

        private static void PatchFixedUpdate(ModuleDefinition MainModule, TypeDefinition Bomblet)
        {
            MethodDefinition CalculateFuzeDetonations = new MethodDefinition("CalculateFuzeDetonations", MethodAttributes.Private, MainModule.TypeSystem.Void);
            CalculateFuzeDetonations.Parameters.Add(new ParameterDefinition("layerMask", ParameterAttributes.None, MainModule.TypeSystem.Int32));
            CalculateFuzeDetonations.Body.GetILProcessor().Emit(OpCodes.Ret);
            Bomblet.Methods.Add(CalculateFuzeDetonations);

            MethodDefinition FixedUpdate = Bomblet.Methods.Where(method => method.IsPrivate && method.Name == "FixedUpdate").First();
            Instruction afterLayerMaskDefined = FixedUpdate.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Stloc_3 && instruction.Previous.OpCode == OpCodes.Or).First().Next;
            ILProcessor processor = FixedUpdate.Body.GetILProcessor();
            // this.CalculateFuzeDetonation(layerMask)
            processor.InsertBefore(afterLayerMaskDefined, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(afterLayerMaskDefined, processor.Create(OpCodes.Ldloc_3));
            processor.InsertBefore(afterLayerMaskDefined, processor.Create(OpCodes.Call, CalculateFuzeDetonations.GetElementMethod()));
        }

        private static TypeDefinition CreateBombletData(ModuleDefinition MainModule, TypeDefinition FuzeType)
        {
            TypeDefinition BombletData = new TypeDefinition("Falcon.Stores", "BombletData", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Serializable);
            BombletData.BaseType = MainModule.TypeSystem.Object;
            FieldDefinition Fuze = new FieldDefinition("Fuze", FieldAttributes.Public, FuzeType.GetElementType());
            FieldDefinition ProximityDistance = new FieldDefinition("ProximityDistance", FieldAttributes.Public, MainModule.TypeSystem.Single);
            FieldDefinition BurstHeight = new FieldDefinition("BurstHeight", FieldAttributes.Public, MainModule.TypeSystem.Single);
            BombletData.Fields.Add(Fuze);
            BombletData.Fields.Add(ProximityDistance);
            BombletData.Fields.Add(BurstHeight);

            MethodReference baseCtor = new MethodReference(".ctor", MainModule.TypeSystem.Void, MainModule.TypeSystem.Object);
            baseCtor.HasThis = true;

            MethodDefinition ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, MainModule.TypeSystem.Void);
            BombletData.Methods.Add(ctor);
            ctor.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
            ctor.Body.GetILProcessor().Emit(OpCodes.Call, MainModule.ImportReference(baseCtor));
            ctor.Body.GetILProcessor().Emit(OpCodes.Ret);

            MainModule.Types.Add(BombletData);

            return BombletData;
        }

        private static void AddBombletToWarheadProperties(ModuleDefinition MainModule, TypeDefinition WarheadProperties, TypeDefinition BombletData) {
            FieldDefinition Bomblet = new FieldDefinition("Bomblet", FieldAttributes.Public, BombletData.GetElementType());
            WarheadProperties.Fields.Add(Bomblet);
            MethodDefinition ctor = WarheadProperties.Methods.Where(method => method.IsConstructor && method.Name == ".ctor").First();
            MethodDefinition Bomblet_ctor = BombletData.Methods.Where(method => method.IsConstructor && method.Name == ".ctor").First();

            ILProcessor processor = ctor.Body.GetILProcessor();
            // Bomblet = new BombletData();
            processor.InsertBefore(ctor.Body.Instructions.First(), processor.Create(OpCodes.Stfld, MainModule.ImportReference(new FieldReference("Bomblet", BombletData, WarheadProperties))));
            processor.InsertBefore(ctor.Body.Instructions.First(), processor.Create(OpCodes.Newobj, MainModule.ImportReference(Bomblet_ctor.GetElementMethod())));
            processor.InsertBefore(ctor.Body.Instructions.First(), processor.Create(OpCodes.Ldarg_0));
        }
    } 
}
