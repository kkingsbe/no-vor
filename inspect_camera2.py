import clr
import System.Reflection

path = r"D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Assembly-CSharp.dll"
asm = System.Reflection.Assembly.LoadFrom(path)

try:
    all_types = asm.GetTypes()
except System.Reflection.ReflectionTypeLoadException as ex:
    all_types = [t for t in ex.Types if t is not None]

camera_mode = [t for t in all_types if t.Name == "CameraMode"]
if camera_mode:
    t = camera_mode[0]
    print("CameraMode values:")
    for name in t.GetEnumNames():
        print("  {}".format(name))

# Also check if CameraStateManager has any fields/methods specifically about scroll
manager = [t for t in all_types if t.Name == "CameraStateManager"][0]
print("\nCameraStateManager fields with 'scroll', 'zoom', 'input', 'mouse', 'fov':")
for f in manager.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
    if any(k in f.Name.lower() for k in ["scroll", "zoom", "input", "mouse", "fov"]):
        print("  {} {}".format(f.FieldType.Name, f.Name))

print("\nCameraStateManager methods with 'scroll', 'zoom', 'input', 'mouse', 'fov':")
for m in manager.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance):
    if any(k in m.Name.lower() for k in ["scroll", "zoom", "input", "mouse", "fov"]):
        print("  {}".format(m.Name))
