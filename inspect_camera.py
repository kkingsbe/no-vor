import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

interesting = [t for t in all_types if t.Name in [
    "CameraStateManager", "CameraControlUI", "CameraBaseState",
    "CameraFreeState", "CameraOrbitState", "CameraChaseState"
]]

for t in interesting:
    print("\n=== {} ===".format(t.Name))
    print("Fields:")
    for f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        print("  {} {}".format(f.FieldType.Name, f.Name))
    print("Methods:")
    for m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        if m.DeclaringType == t:
            print("  {}".format(m.Name))
    print("Properties:")
    for p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static):
        print("  {} {}".format(p.PropertyType.Name, p.Name))
