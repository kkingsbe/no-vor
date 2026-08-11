import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

# Find all methods that reference allowInputs
manager = [t for t in all_types if t.Name == "CameraStateManager"][0]
allow_inputs_field = manager.GetField("allowInputs")

print("Types/methods that reference allowInputs:")
for t in all_types:
    try:
        for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
            body = m.GetMethodBody()
            if body is None:
                continue
            # This is a crude check - real IL analysis would be better
            # But we can at least check field references in the type
    except:
        pass

# Better approach: search for 'allowInputs' in method names or check specific types we care about
for t in [manager] + [tt for tt in all_types if tt.Name.startswith("Camera") and "State" in tt.Name]:
    print("\n=== {} ===".format(t.Name))
    for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
        # Check if method IL mentions allowInputs
        try:
            body = m.GetMethodBody()
            if body is None:
                continue
            # GetMethodBody doesn't give us IL easily, so let's just print methods that might use it
            if any(k in m.Name.lower() for k in ["input", "update", "fixedupdate", "check"]):
                print("  {}".format(m.Name))
        except:
            pass
