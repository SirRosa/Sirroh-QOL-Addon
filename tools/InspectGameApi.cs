using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length == 2 && args[0] == "--verify-swimming")
{
    using var target = AssemblyDefinition.ReadAssembly(args[1]);
    foreach (var (name, method) in new [] {
      ("Character", "IsSwimming"),
      ("Humanoid", "EquipItem"),
      ("Humanoid", "UpdateEquipment")
    })
    {
        var type = target.MainModule.Types.FirstOrDefault(t => t.Name == name)
            ?? throw new InvalidOperationException(name + " missing");
        var methods = type.Methods.Where(m => m.Name == method).ToArray();
        if (methods.Length == 0) throw new InvalidOperationException(name + "." + method + " missing");
        foreach (var entry in methods)
        {
            var swimmingCalls = entry.HasBody
                ? entry.Body.Instructions.Count(i => i.Operand is MethodReference callee && callee.Name == "IsSwimming")
                : 0;
            Console.WriteLine($"{name}.{method}: {entry.FullName}; Calls IsSwimming={swimmingCalls}");
        }
    }
    Console.WriteLine("PASS: Valheim exposes swimming equipment patch targets");
    return;
}
var asm = AssemblyDefinition.ReadAssembly(args[0]);
string[] types = {"InventoryGui", "CraftingStation", "Player", "Character", "Sign", "Fireplace", "Player/Food"};
var root = asm.MainModule.Types;
foreach (var name in types)
{
    var t = root.FirstOrDefault(x => x.Name == name || x.FullName == name.Replace('/', '+'))
         ?? root.SelectMany(x => x.NestedTypes).FirstOrDefault(x => x.Name == name.Split('/').Last() && x.DeclaringType?.Name == "Player");
    if (t is null) { Console.WriteLine("TYPE MISSING: " + name); continue; }
    Console.WriteLine("\n### TYPE " + t.FullName);
    foreach (var f in t.Fields.Where(f => name is "Player/Food" || f.Name.Contains("food", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("craft", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("repair", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("slide", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("ground", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("text", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("stamina", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine("FIELD " + f.FieldType + " " + f.Name);
    foreach (var m in t.Methods.Where(m => (name switch {
      "InventoryGui" => new []{"Show","UpdateRepair","HaveRepairableItems","RepairOneItem","CanRepair","RepairItems","UpdateCraftingPanel","UpdateRepairPanel"}.Any(x => m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      "CraftingStation" => new []{"Interact","GetLevel"}.Any(x=>m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      "Player" => new []{"UpdateFood","UpdateEnvStatusEffects","UpdateStats"}.Any(x=>m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      "Character" => new []{"ApplySlide","UpdateMotion","UpdateGroundContact","UpdateGroundTilt"}.Any(x=>m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      "Sign" => new []{"Awake","UpdateText","OnCheckPermissionCompleted","SetText"}.Any(x=>m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      "Fireplace" => new []{"IsBurning","Awake","UpdateFireplace"}.Any(x=>m.Name.Contains(x, StringComparison.OrdinalIgnoreCase)),
      _ => false
    })))
    {
        Console.WriteLine("\nMETHOD " + m.FullName);
        if (!m.HasBody) continue;
        foreach (var ins in m.Body.Instructions)
            Console.WriteLine("   " + ins);
    }
}
