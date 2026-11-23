using Mono.Cecil;

// Inspect the user's installed DLLs without executing Unity or modifying the game.
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CompatibilityCheck <Bonehilda.dll> <game Managed directory> [dependency directories...]");
    return 2;
}
var resolver = new DefaultAssemblyResolver();
foreach (var directory in args.Skip(1)) resolver.AddSearchDirectory(Path.GetFullPath(directory));
using var plugin = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver });
var errors = new List<string>();
var count = 0;
foreach (var member in plugin.MainModule.GetMemberReferences())
{
    var scope = member.DeclaringType.Scope.Name;
    if (!(scope.StartsWith("assembly_") || scope.StartsWith("UnityEngine") || scope == "Jotunn")) continue;
    try
    {
        object? resolved = member switch { MethodReference method => method.Resolve(), FieldReference field => field.Resolve(), _ => member };
        if (resolved == null) errors.Add(member.FullName);
        count++;
    }
    catch (Exception e) { errors.Add($"{member.FullName}: {e.Message}"); }
}
using var game = AssemblyDefinition.ReadAssembly(Path.Combine(args[1], "assembly_valheim.dll"));
using var utils = AssemblyDefinition.ReadAssembly(Path.Combine(args[1], "assembly_utils.dll"));
TypeDefinition? Find(string name) => game.MainModule.GetType(name) ?? utils.MainModule.GetType(name);
void Method(string type, string name, string returns, params string[][] signatures)
{
    count++;
    if (Find(type)?.Methods.Any(m => m.Name == name && returns.Split('|').Contains(m.ReturnType.FullName)
        && signatures.Any(signature => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(signature))) != true)
        errors.Add($"Reflection contract: {type}.{name}");
}
void Field(string type, string name, string expectedType)
{
    count++;
    if (Find(type)?.Fields.Any(f => f.Name == name && f.FieldType.FullName == expectedType) != true)
        errors.Add($"Reflection contract: {type}.{name}");
}
// Private members bypass normal CLR reference resolution. Pin the signatures
// used by supply, save recovery, sound, and seating adapters explicitly.
Method("Fireplace", "RPC_AddFuel", "System.Void", new[] { "System.Int64" });
Method("Smelter", "RPC_AddFuel", "System.Void", new[] { "System.Int64" });
Method("Smelter", "RPC_AddOre", "System.Void", new[] { "System.Int64", "System.String" }, new[] { "System.Int64", "System.String", "System.Boolean" });
Method("Fermenter", "RPC_AddItem", "System.Void", new[] { "System.Int64", "System.Int32" }, new[] { "System.Int64", "System.Int32", "System.Boolean" });
Method("Fermenter", "GetContent", "System.Int32", Array.Empty<string>());
Method("ShieldGenerator", "RPC_AddFuel", "System.Void", new[] { "System.Int64" });
Method("ShieldGenerator", "GetFuel", "System.Single", Array.Empty<string>());
Method("Turret", "RPC_AddAmmo", "System.Void", new[] { "System.Int64", "System.String" });
Method("Container", "CheckForChanges", "System.Void", Array.Empty<string>());
Method("Container", "Save", "System.Void", Array.Empty<string>());
Method("Door", "CanInteract", "System.Boolean", Array.Empty<string>());
Method("Chair", "IsInUse", "System.Boolean", Array.Empty<string>());
Method("ItemStand", "GetAttachedItem", "System.String|System.Int32", Array.Empty<string>());
Method("EffectList", "Create", "UnityEngine.GameObject[]",
    new[] { "UnityEngine.Vector3", "UnityEngine.Quaternion", "UnityEngine.Transform", "System.Single", "System.Int32" },
    new[] { "UnityEngine.Vector3", "UnityEngine.Quaternion", "UnityEngine.Transform", "System.Single", "System.Int32", "ZDOID" });
Method("Ragdoll", "Setup", "System.Void",
    new[] { "UnityEngine.Vector3", "System.Single", "System.Single", "System.Single", "CharacterDrop", "System.Int32" },
    new[] { "UnityEngine.Vector3", "System.Single", "System.Single", "System.Single", "CharacterDrop", "System.Int32", "System.Boolean" });
Field("Inventory", "m_height", "System.Int32");
Field("PrivateArea", "m_allAreas", "System.Collections.Generic.List`1<PrivateArea>");
Field("ZDOMan", "m_objectsByID", "System.Collections.Generic.Dictionary`2<ZDOID,ZDO>");
Field("ItemStand", "m_visualItem", "UnityEngine.GameObject");

// Verify Harmony targets too; private patched methods do not occur as CLR references.
foreach (var type in plugin.MainModule.Types)
foreach (var patch in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch" && a.ConstructorArguments.Count >= 2))
{
    if (patch.ConstructorArguments[0].Value is not TypeReference target || patch.ConstructorArguments[1].Value is not string method) continue;
    count++;
    try
    {
        var methods = target.Resolve().Methods.Where(m => m.Name == method);
        if (patch.ConstructorArguments.Count > 2 && patch.ConstructorArguments[2].Value is CustomAttributeArgument[] parameters)
            methods = methods.Where(m => m.Parameters.Select(p => p.ParameterType.FullName)
                .SequenceEqual(parameters.Select(p => ((TypeReference)p.Value).FullName)));
        if (!methods.Any()) errors.Add($"Harmony target: {target.FullName}.{method}");
    }
    catch (Exception e) { errors.Add($"Harmony target: {type.FullName}: {e.Message}"); }
}
foreach (var error in errors) Console.WriteLine($"MISSING: {error}");
Console.WriteLine($"Checked {count} installed API references, reflection contracts, and Harmony targets; {errors.Count} unresolved.");
Console.WriteLine("This is a static API check, not a dedicated-server or multiplayer gameplay test.");
return errors.Count == 0 ? 0 : 1;
