using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;








if (args.Length == 2 && args[0] == "--inspect-vanilla-ui-dimming")
{
    using var dimmingAssembly = AssemblyDefinition.ReadAssembly(args[1]);
    string[] typeNames = { "InventoryGui", "Hud", "EnvMan", "GuiBar", "GuiScaler",
        "GuiInputField", "InventoryGrid", "FejdStartup", "Menu",
        "GameCamera", "Game", "Player", "Minimap", "UITime", "UISettings",
        "UIGroupHandler", "UITooltip", "GuiPicker", "Settings", "GameCamera",
        "Lighting", "Utils", "DayCycle", "DodgeableUI" };
    string[] needles = { "set_color", "set_alpha", "SetColor", "SetFloat",
       "SetGlobalColor", "SetGlobalFloat", "GetGlobalColor", "GetGlobalFloat",
       "GetAmbient", "GetLight", "GetSky", "IsNight", "IsDay", "GetCurrentEnvironment",
       "SetGlobalVector", "SetGlobalTexture", "set_material", "set_shader", "get_color" };
    var dimmingTypes=dimmingAssembly.MainModule.Types;
    foreach(var type in dimmingTypes.Where(t=>typeNames.Contains(t.Name)))
    {
        Console.WriteLine("### TYPE "+type.FullName);
        foreach(var field in type.Fields.Where(f=>
            f.Name.IndexOf("color",StringComparison.OrdinalIgnoreCase)>=0 ||
            f.Name.IndexOf("dim",StringComparison.OrdinalIgnoreCase)>=0 ||
            f.Name.IndexOf("light",StringComparison.OrdinalIgnoreCase)>=0 ||
            f.Name.IndexOf("night",StringComparison.OrdinalIgnoreCase)>=0 ||
            f.Name.IndexOf("material",StringComparison.OrdinalIgnoreCase)>=0))
            Console.WriteLine("FIELD "+field.FullName);
        foreach(var method in type.Methods)
        {
            if(!method.HasBody)continue;
            var inst=method.Body.Instructions;
            var calls=inst.Select((il,i)=>new {il,i})
              .Where(x=>x.il.Operand is MethodReference m && needles.Any(
                  n=>m.Name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0))
              .ToArray();
            bool named=method.Name.IndexOf("color",StringComparison.OrdinalIgnoreCase)>=0
                || method.Name.IndexOf("brightness",StringComparison.OrdinalIgnoreCase)>=0
                || method.Name.IndexOf("dark",StringComparison.OrdinalIgnoreCase)>=0
                || method.Name.IndexOf("light",StringComparison.OrdinalIgnoreCase)>=0;
            if(!named && calls.Length==0)continue;
            Console.WriteLine("METHOD "+method.FullName+" callCount="+calls.Length);
            foreach(var c in calls)
            {
                Console.WriteLine("  CALL "+c.il);
                int from=Math.Max(0,c.i-4), to=Math.Min(inst.Count-1,c.i+3);
                for(int j=from;j<=to;j++)Console.WriteLine("    "+inst[j]);
            }
        }
    }
    // Search ALL game code for global shader globals. If Valheim fades the
    // inventory via shader property, this pinpoints the authoritative writer.
    foreach(var type in dimmingTypes)
        foreach(var method in type.Methods.Where(m=>m.HasBody))
        {
            foreach(var il in method.Body.Instructions)
            {
                if(il.Operand is MethodReference callee && (
                    callee.Name.StartsWith("SetGlobal",StringComparison.Ordinal) ||
                    callee.Name.StartsWith("GetGlobal",StringComparison.Ordinal)) &&
                    callee.DeclaringType.Name=="Shader")
                    Console.WriteLine("GLOBAL SHADER "+method.FullName+" :: "+il);
            }
        }
    return;
}

if (args.Length == 2 && args[0] == "--diagnose-inventory-recipe-null")
{
    using var a = AssemblyDefinition.ReadAssembly(args[1]);
    var t=a.MainModule.Types.First(x=>x.Name=="InventoryGui");
    string[] targets={"Show","SetupCrafting","UpdateCraftingPanel","UpdateRecipeList","Awake","Update"};
    foreach(var f in t.Fields.Where(f=>f.Name.Contains("recipe",StringComparison.OrdinalIgnoreCase)
       || f.Name.Contains("craft",StringComparison.OrdinalIgnoreCase)
       || f.Name.Contains("player",StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine("FIELD "+f.FullName);
    foreach(var m in t.Methods.Where(x=>targets.Contains(x.Name)))
    {
        Console.WriteLine("METHOD "+m.FullName);
        if(!m.HasBody)continue;
        foreach(var il in m.Body.Instructions)
            Console.WriteLine("  "+il);
    }
    return;
}

if (args.Length == 2 && args[0] == "--verify-qol-assets")
{
    using var a = AssemblyDefinition.ReadAssembly(args[1]);
    foreach (string name in new [] {"border","trash","trashmask","trashingborder","RecyclingPanel","ui_sort","ui_favorite","ui_quickstack","ui_restock","ui_recycle","ui_wood","ui_favorite_gold","ui_favorite_halo"})
    {
        string logical = "Sirroh.Qol.Assets." + name + ".rgba";
        var asset = a.MainModule.Resources.FirstOrDefault(r=>r.Name == logical) as EmbeddedResource;
        if (asset == null) throw new InvalidOperationException("Asset missing from final DLL: "+logical);
        byte[] bytes = asset.GetResourceData();
        if (bytes.Length < 8) throw new InvalidOperationException("Asset is incomplete: "+logical);
        int width = BitConverter.ToInt32(bytes,0), height=BitConverter.ToInt32(bytes,4);
        if (width < 1 || height < 1 || width > 2048 || height > 2048 ||
            bytes.Length != 8 + width * height * 4)
             throw new InvalidOperationException("Invalid embedded PNG conversion: "+logical);
        Console.WriteLine(logical + " " + width + "x" + height + " confirmed in DLL.");
    }
    Console.WriteLine("PASS: all 13 QOL resources embedded, including gold Favorite and radial halo.");
    return;
}

if (args.Length == 2 && args[0] == "--inventory-qol-api")
{
 using var a=AssemblyDefinition.ReadAssembly(args[1]);
 foreach(var name in new[]{"Inventory","InventoryGui","InventoryGrid","Container","ObjectDB","Recipe","ItemDrop","Player","Humanoid"})
 {
  var t=a.MainModule.Types.FirstOrDefault(t=>t.Name==name);
  if(t==null)continue;
  Console.WriteLine("TYPE "+name);
  foreach(var f in t.Fields.Where(f=>f.Name.Contains("inventory")||f.Name.Contains("drag")||f.Name.Contains("nview")||f.Name.Contains("inUse")||f.Name.Contains("recipe")||f.Name.Contains("player")||f.Name.Contains("button")||f.Name.Contains("element")))
    Console.WriteLine("FIELD "+f.FullName);
  string[] methods={"AddItem","RemoveItem","CanAddItem","MoveItemToThis","Changed","GetEmptySlots","FindFreeStackSpace","GetAllItems","GetItemAt","GetItem","ContainsItem","Show","Hide","UpdateItemDrag","SetupDragItem","Update","Awake","UpdateCraftingPanel","OnLeftDown","OnRightDown","UpdateGui","GetButton","GetButtonPos","OnDestroy","CheckAccess","IsInUse","SetInUse","Save","Load","GetRecipe","GetItemPrefab","RemoveEquipAction"};
  foreach(var method in t.Methods.Where(m=>methods.Contains(m.Name)))
    Console.WriteLine("METHOD "+method.FullName);
 }
 return;
}

if (args.Length == 2 && args[0] == "--analyze-simulation-radius")
{
    using var assembly = AssemblyDefinition.ReadAssembly(args[1]);
    string[] radiusTypes = { "SimulationDistance", "ZNetScene", "ZDOMan", "ZNet", "ZoneSystem" };
    foreach (var t in assembly.MainModule.Types.Where(t=>radiusTypes.Contains(t.Name)))
    {
        Console.WriteLine("TYPE " + t.FullName);
        foreach (var f in t.Fields.Where(f=>t.Name=="SimulationDistance" ||
            f.Name.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            f.Name.Contains("distance", StringComparison.OrdinalIgnoreCase) ||
            f.Name.Contains("radius", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine("FIELD " + f.FullName + (f.HasConstant ? " CONSTANT="+f.Constant : ""));
        foreach (var m in t.Methods.Where(m => m.Name == "FindSectorObjects" ||
            m.Name == "GetSyncedSimulationDistance" ||
            m.Name == "GetSimulationDistance" ||
            m.Name == "PointInsideActiveArea" ||
            m.Name == "GetZone" ||
            m.Name == "CreateDestroyObjects" ||
            m.Name == "CreateObjects" ||
            m.Name == "RemoveObjects" ||
            m.Name == ".ctor" && t.Name=="SimulationDistance" ||
            m.Name.Contains("SimulationDistance")))
        {
            Console.WriteLine("METHOD " + m.FullName);
            if(m.HasBody) foreach(var i in m.Body.Instructions) Console.WriteLine("  "+i);
        }
    }
    Console.WriteLine("SimulationDistance method usage");
    foreach(var t in assembly.MainModule.Types.Where(t=> t.Name=="ZNet" || t.Name=="ZDOMan" || t.Name=="ZoneSystem"))
     foreach(var m in t.Methods.Where(m=>m.HasBody))
      if(m.Body.Instructions.Any(i=>i.Operand is MethodReference mr && mr.DeclaringType.Name=="SimulationDistance"))
      {
       Console.WriteLine("USES " + m.FullName);
       foreach(var i in m.Body.Instructions) Console.WriteLine("  "+i);
      }
    return;
}

if (args.Length == 2 && args[0] == "--diagnose-player-destruction")
{
    using var assembly = AssemblyDefinition.ReadAssembly(args[1]);
    string[] relevantTypes = { "ZNetScene", "ZoneSystem", "ZNet", "ZDOMan", "Player" };
    string[] wanted = { "CreateDestroyObjects", "RemoveObjects", "DestroyObjects", "IsObjectInRange", "IsInActiveArea", "OutsideActiveArea",
      "GetReferencePosition", "SetReferencePosition", "GetZone", "GetActiveArea", "OnDestroy", "SetLocalPlayer", "LateUpdate", "Update" };
    foreach (var type in assembly.MainModule.Types.Where(t => relevantTypes.Contains(t.Name)))
    {
        Console.WriteLine("TYPE " + type.FullName);
        foreach (var f in type.Fields.Where(f=> f.Name.Contains("active", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("distant", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("zone", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("sector", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("area", StringComparison.OrdinalIgnoreCase)))
             Console.WriteLine("FIELD " + f.FullName + (f.HasConstant ? " CONSTANT="+f.Constant : ""));
        foreach (var m in type.Methods.Where(m => wanted.Any(n => m.Name.Equals(n, StringComparison.OrdinalIgnoreCase))
            || (type.Name=="ZNetScene" && (m.Name.Contains("Remove") || m.Name.Contains("Destroy") || m.Name.Contains("Range") || m.Name.Contains("Zone") || m.Name.Contains("Outside")))
            || (type.Name=="ZoneSystem" && (m.Name.Contains("ActiveArea") || m.Name.Contains("Zone")))))
        {
            Console.WriteLine("METHOD " + m.FullName);
            if (!m.HasBody) continue;
            foreach(var i in m.Body.Instructions) Console.WriteLine("  "+i);
        }
    }
    Console.WriteLine("CALLS in player destruction logging");
    foreach(var t in assembly.MainModule.Types.Where(t=>relevantTypes.Contains(t.Name)))
      foreach(var m in t.Methods.Where(m=>m.HasBody))
        foreach(var instr in m.Body.Instructions.Where(i=>i.Operand is string v && v.Contains("Local player destroyed")))
            Console.WriteLine("LOG " + m.FullName + " @ "+instr);
    return;
}

if (args.Length == 2 && args[0] == "--inspect-camera-streaming")
{
    using var game = AssemblyDefinition.ReadAssembly(args[1]);
    var targets = new Dictionary<string, string[]> {
        ["ZoneSystem"] = new [] { "Update", "UpdateZones", "CreateLocalZones", "CreateZone", "GetZone", "GetGroundHeight", "PokeLocalZone" },
        ["ZNetScene"] = new [] { "Update", "CreateObjects", "CreateDestroyObjects", "UpdateReferencePosition", "SetReferencePosition", "GetReferencePosition" },
        ["ZNet"] = new [] { "Update", "SetReferencePosition", "GetReferencePosition", "GetReferencePositionPublic", "GetReferencePositionForPeer" },
        ["ZDOMan"] = new [] { "Update", "FindSectorObjects", "FindSectorObjectsAsync", "GetSectors" },
        ["Player"] = new [] { "Pickup", "AutoPickup", "Update", "UpdateLocalPlayer" },
        ["Humanoid"] = new [] { "Pickup" },
        ["ItemDrop"] = new [] { "CanPickup", "RequestOwn", "Pickup", "Load", "InTar" }
    };
    foreach (var pair in targets)
    {
        var t = game.MainModule.Types.FirstOrDefault(x=>x.Name == pair.Key);
        if (t == null) continue;
        Console.WriteLine("### TYPE " + t.FullName);
        foreach (var field in t.Fields.Where(f=> f.Name.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || f.Name.Contains("local", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("zone", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("range", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine("FIELD " + field);
        foreach(var met in t.Methods.Where(m=>pair.Value.Any(x => x == m.Name)))
        {
            Console.WriteLine("METHOD " + met.FullName);
            if (met.HasBody)
                foreach (var i in met.Body.Instructions)
                    Console.WriteLine("  " + i);
        }
    }
    Console.WriteLine("CALLS for reference position or local zone management");
    foreach(var t in game.MainModule.Types.Where(x=>x.Name == "ZoneSystem" || x.Name=="ZNetScene" || x.Name=="ZNet" || x.Name=="ZDOMan" || x.Name=="Player"))
        foreach(var m in t.Methods.Where(m=>m.HasBody))
        {
            var calls = m.Body.Instructions.Where(i=>i.Operand is MethodReference mr &&
                (mr.Name.Contains("ReferencePos", StringComparison.OrdinalIgnoreCase)
                || mr.Name.Contains("ReferencePosition", StringComparison.OrdinalIgnoreCase)
                || mr.Name == "CreateLocalZones" || mr.Name=="PokeLocalZone")).ToArray();
            if(calls.Length > 0) Console.WriteLine(m.FullName + ": " + string.Join(" | ",calls.Select(x=>x.ToString())));
        }
    return;
}

if (args.Length == 2 && args[0] == "--inspect-swim")
{
    using var game = AssemblyDefinition.ReadAssembly(args[1]);
    string[] relevant = { "Character", "Humanoid", "Player", "VisEquipment", "PlayerController" };
    string[] needles = { "IsSwimming", "HideHandItems", "ShowHandItems", "UpdateEquipment", "EquipItem",
        "UpdateMotion", "InWater", "UpdateSwimming", "UpdateHandItems", "UnequipItem" };
    foreach (var typ in game.MainModule.Types.Where(t => relevant.Contains(t.Name)))
    {
        Console.WriteLine("TYPE " + typ.FullName);
        foreach (var field in typ.Fields.Where(f => f.Name.Contains("hide", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("equip", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("swim", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine("FIELD " + field.FullName);
        foreach (var met in typ.Methods.Where(m => needles.Any(n => m.Name.Contains(n, StringComparison.OrdinalIgnoreCase))))
        {
            Console.WriteLine("\nMETHOD " + met.FullName);
            if (met.HasBody) foreach (var il in met.Body.Instructions) Console.WriteLine("  " + il);
        }
    }
    var player = game.MainModule.Types.First(x => x.Name == "Player");
    var update = player.Methods.First(x => x.Name == "Update" && x.HasBody);
    Console.WriteLine("\nPLAYER.UPDATE around HideHandItems and ShowHandItems");
    var instructions = update.Body.Instructions;
    for (int i=0; i<instructions.Count; i++)
    {
        if (instructions[i].Operand is MethodReference mr &&
             (mr.Name == "HideHandItems" || mr.Name == "ShowHandItems"))
        {
            for (int j=Math.Max(0,i-34); j<=Math.Min(instructions.Count-1,i+36); j++)
                Console.WriteLine("  " + instructions[j]);
        }
    }
    Console.WriteLine("\nCALL SITES involving HideHandItems / ShowHandItems / UnequipItem / IsSwimming");
    foreach (var typ in game.MainModule.Types)
        foreach (var met in typ.Methods.Where(m => m.HasBody))
        {
            var found = met.Body.Instructions.Where(i => i.Operand is MethodReference mr &&
                (mr.Name == "HideHandItems" || mr.Name == "ShowHandItems" || mr.Name == "UnequipItem" || mr.Name == "IsSwimming")).ToArray();
            if (found.Length > 0) Console.WriteLine(met.FullName + ": " + string.Join(" ; ",found.Select(i=>i.ToString())));
        }
    return;
}

if (args.Length == 2 && args[0] == "--verify-swimming")
{
    using var target = AssemblyDefinition.ReadAssembly(args[1]);
    foreach (var (name, method) in new [] {
      ("Character", "IsSwimming"),
      ("Humanoid", "EquipItem"),
      ("Humanoid", "UpdateEquipment"),
      ("Player", "Update")
    })
    {
        var type = target.MainModule.Types.FirstOrDefault(t => t.Name == name)
            ?? throw new InvalidOperationException(name + " missing");
        var methods = type.Methods.Where(m => m.Name == method).ToArray();
        if (methods.Length != 1) throw new InvalidOperationException(name + "." + method + " missing or ambiguous");
        foreach (var entry in methods)
        {
            var swimmingCalls = entry.HasBody
                ? entry.Body.Instructions.Count(i => i.Operand is MethodReference callee && callee.Name == "IsSwimming")
                : 0;
            Console.WriteLine($"{name}.{method}: {entry.FullName}; Calls IsSwimming={swimmingCalls}");
            if (name != "Character" && swimmingCalls != 1) throw new InvalidOperationException("Expected exactly one IsSwimming in " + name + "." + method);
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
