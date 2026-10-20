using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Also check the final compiled DLL, not just generated files on disk.
if (args.Length == 2 && args[0] == "--verify-sign-resources")
{
    using var dll = AssemblyDefinition.ReadAssembly(args[1]);
    var resources = dll.MainModule.Resources.ToDictionary(r => r.Name);
    foreach (char code in "BSGRY")
    {
        string key = $"Sirroh.Qol.Signs.{code}.rgba";
        if (!resources.TryGetValue(key, out var value) ||
            value is not EmbeddedResource bytes ||
            bytes.GetResourceData().Length != 256 * 256 * 4)
            throw new InvalidOperationException("Missing or incomplete embedded texture: " + key);
        Console.WriteLine(key + ": embedded RGBA 256x256 OK");
    }
    Console.WriteLine("PASS: five valid embedded sign surfaces in compiled SirrohQolAddon.dll");
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
